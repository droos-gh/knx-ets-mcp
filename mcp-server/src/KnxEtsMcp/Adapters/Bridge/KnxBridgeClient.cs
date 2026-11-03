using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.BusinessLogic.Gateways;

namespace KnxEtsMcp.Adapters.Bridge;

/// <summary>
/// <see cref="IKnxBridge"/> over an <see cref="ITransport"/>:
/// <list type="bullet">
/// <item>builds the request envelope (UUID request id, session token, method, params);</item>
/// <item>adds a fresh UUID idempotency key per mutating call, reused only when this
/// client itself retries after a transport failure (docs/protocol.md);</item>
/// <item>validates the response envelope (shape, id match) and maps protocol errors;</item>
/// <item>on a transport failure closes, reconnects and retries once (with the token the
/// reconnect read, which changes when the AddIn restarted);</item>
/// <item>closes the connection whenever the stream may be out of step (cancellation
/// mid-exchange, a response that is not for this request), so the next call starts on a
/// fresh connection instead of reading a stale response;</item>
/// <item>serializes all exchanges, because the transport is a single connection.</item>
/// </list>
/// The transport is connected lazily on the first request.
/// </summary>
public sealed class KnxBridgeClient : IKnxBridge, IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _connected;

    public KnxBridgeClient(ITransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task<Result<JsonNode?>> RequestAsync(
        string method,
        JsonObject? parameters = null,
        string? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_connected)
            {
                var connect = await TryConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connect.IsSuccess)
                    return Result<JsonNode?>.Fail(connect.Error!);
            }

            // The token is (re)stamped on every send, see SendOnceAsync.
            var request = new JsonObject
            {
                ["id"] = requestId,
                ["method"] = method,
                ["params"] = parameters ?? new JsonObject(),
            };
            // Fresh key per operation; reused across the retry of THIS call only.
            if (ProtocolMethods.Mutating.Contains(method))
                request["idempotencyKey"] = Guid.NewGuid().ToString();
            if (expectedRevision is not null)
                request["expectedProjectRevision"] = expectedRevision;

            var response = await SendWithRetryAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccess && !IsResponseFor(response.Value, requestId))
            {
                // A stale or misrouted line: the stream is out of step with our requests,
                // so drop the connection rather than letting every later call lag by one.
                await CloseQuietlyAsync().ConfigureAwait(false);
            }
            return response.Bind(r => ParseResponse(r, requestId));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Result<bool>> TryConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _connected = true;
            return Result<bool>.Ok(true);
        }
        catch (TransportException ex)
        {
            return Result<bool>.Fail(BridgeError.Internal, $"Cannot reach the ETS AddIn: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends; on a transport failure closes the (possibly de-synced) connection,
    /// reconnects and retries once with the SAME request (same idempotency key), so the
    /// AddIn deduplicates a lost-response scenario.
    /// </summary>
    private async Task<Result<JsonNode?>> SendWithRetryAsync(JsonObject request, CancellationToken cancellationToken)
    {
        try
        {
            return Result<JsonNode?>.Ok(await SendOnceAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (TransportException)
        {
            await CloseQuietlyAsync().ConfigureAwait(false);
        }

        var reconnect = await TryConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!reconnect.IsSuccess)
            return Result<JsonNode?>.Fail(BridgeError.Internal, "Transport failed and reconnect unsuccessful");

        try
        {
            return Result<JsonNode?>.Ok(await SendOnceAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (TransportException)
        {
            await CloseQuietlyAsync().ConfigureAwait(false);
            return Result<JsonNode?>.Fail(BridgeError.Internal, "Transport failed after reconnect");
        }
    }

    /// <summary>
    /// One exchange with the current connection's token. If the caller cancels while the
    /// request may already be on the wire, the AddIn's reply would arrive later on this
    /// connection and be read by the NEXT request -- so the connection is closed before
    /// the cancellation propagates.
    /// </summary>
    private async Task<JsonNode?> SendOnceAsync(JsonObject request, CancellationToken cancellationToken)
    {
        request["token"] = _transport.Token;
        try
        {
            return await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await CloseQuietlyAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task CloseQuietlyAsync()
    {
        _connected = false;
        try
        {
            await _transport.CloseAsync().ConfigureAwait(false);
        }
        catch (TransportException)
        {
            // Best effort: we are about to reconnect anyway.
        }
    }

    /// <summary>Validates the envelope and maps it to the protocol result or error.</summary>
    internal static Result<JsonNode?> ParseResponse(JsonNode? response, string expectedId)
    {
        if (response is not JsonObject envelope)
            return Result<JsonNode?>.Fail(BridgeError.Internal, "Invalid response: not a JSON object");

        if (!IsString(envelope["id"], expectedId))
            return Result<JsonNode?>.Fail(BridgeError.Internal, "Response id mismatch (stale or misrouted response)");

        var ok = envelope["ok"] is JsonValue okValue && okValue.TryGetValue<bool>(out var b) && b;
        if (ok)
        {
            if (!envelope.ContainsKey("result"))
                return Result<JsonNode?>.Fail(BridgeError.Internal, "Success response missing result field");
            return Result<JsonNode?>.Ok(envelope["result"]?.DeepClone());
        }

        if (!envelope.ContainsKey("error"))
            return Result<JsonNode?>.Fail(BridgeError.Internal, "Error response missing error field");
        if (envelope["error"] is not JsonObject error)
            return Result<JsonNode?>.Fail(BridgeError.Internal, "Malformed error in response");

        return Result<JsonNode?>.Fail(
            StringOr(error["code"], BridgeError.Internal),
            StringOr(error["message"], "Unknown error from AddIn"));
    }

    /// <summary>True when <paramref name="response"/> is an envelope carrying <paramref name="requestId"/>.</summary>
    private static bool IsResponseFor(JsonNode? response, string requestId)
        => response is JsonObject envelope && IsString(envelope["id"], requestId);

    private static bool IsString(JsonNode? node, string expected)
        => node is JsonValue v && v.TryGetValue<string>(out var s) && s == expected;

    private static string StringOr(JsonNode? node, string fallback)
        => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
