using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

/// <summary>
/// Production transport on the ETS PC: reads the pipe name and session token from
/// %LOCALAPPDATA%\knx-ets-bridge\session.json (written by the AddIn, readable only by
/// the current user) and talks over that Windows named pipe.
/// After a read timeout the connection is discarded, never reused: the caller
/// (KnxBridgeClient) closes and reconnects before retrying.
/// </summary>
public sealed class NamedPipeTransport : ITransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private readonly string _sessionFile;
    private NamedPipeClientStream? _pipe;
    private JsonLineStream? _lines;

    /// <param name="sessionFile">Defaults to %LOCALAPPDATA%\knx-ets-bridge\session.json.</param>
    public NamedPipeTransport(string? sessionFile = null)
    {
        _sessionFile = sessionFile ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "knx-ets-bridge", "session.json");
    }

    public string Token { get; private set; } = "";

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await CloseAsync().ConfigureAwait(false);
        var (pipeName, token) = ReadSession();

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);
        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new TransportException("Timed out connecting to the ETS AddIn pipe (is ETS running with the project open?)");
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new TransportException($"Cannot connect to the ETS AddIn pipe: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            // The AddIn restricts the pipe to the Windows user running ETS.
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new TransportException(
                "Access to the ETS AddIn pipe was denied: run the MCP server as the same Windows "
              + $"user as ETS, with the same elevation (both as administrator or neither). {ex.Message}", ex);
        }
        catch (OperationCanceledException)
        {
            // Caller cancellation: do not leak the pipe handle.
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        Token = token;
        _pipe = pipe;
        _lines = new JsonLineStream(pipe);
    }

    private (string PipeName, string Token) ReadSession()
    {
        try
        {
            var session = JsonNode.Parse(File.ReadAllText(_sessionFile)) as JsonObject;
            var pipeName = (string?)session?["pipeName"];
            var token = (string?)session?["token"];
            if (string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(token))
                throw new TransportException($"{_sessionFile} has no pipeName/token (update the ETS AddIn)");
            return (pipeName, token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            throw new TransportException(
                $"Cannot read {_sessionFile} (is ETS running with the AddIn and a project open?): {ex.Message}", ex);
        }
    }

    public async Task<JsonNode?> SendAsync(JsonObject request, CancellationToken cancellationToken = default)
    {
        var lines = _lines ?? throw new TransportException("Transport not connected. Call ConnectAsync() first.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);
        try
        {
            await lines.WriteAsync(request, timeout.Token).ConfigureAwait(false);
            return await lines.ReadAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransportException("Timed out waiting for response from ETS AddIn");
        }
        catch (IOException ex)
        {
            throw new TransportException($"ETS AddIn pipe failed: {ex.Message}", ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw new TransportException("ETS AddIn pipe was closed", ex);
        }
    }

    public async Task CloseAsync()
    {
        _lines = null;
        if (_pipe is not null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
