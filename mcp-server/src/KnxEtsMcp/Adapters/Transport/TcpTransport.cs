using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

/// <summary>
/// Connects to the AddIn's optional TCP endpoint (MCP server on another machine than
/// ETS). Same JSON-line protocol and token as the pipe; the token is configured by hand
/// (KNX_BRIDGE_TOKEN) because a remote machine cannot read session.json.
/// No TLS: trusted LAN only.
/// </summary>
public sealed class TcpTransport : ITransport
{
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(30);

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _readTimeout;
    private TcpClient? _client;
    private JsonLineStream? _lines;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is outside 1..65535.</exception>
    public TcpTransport(string host, int port, string token,
                        TimeSpan? connectTimeout = null, TimeSpan? readTimeout = null)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, $"Invalid port number: {port}");
        _host = host;
        _port = port;
        Token = token;
        _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
        _readTimeout = readTimeout ?? DefaultReadTimeout;
    }

    public string Token { get; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await CloseAsync().ConfigureAwait(false);
        var client = new TcpClient { NoDelay = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);
        try
        {
            await client.ConnectAsync(_host, _port, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            throw new TransportException($"Timed out connecting to ETS AddIn at {_host}:{_port}");
        }
        catch (SocketException ex)
        {
            client.Dispose();
            throw new TransportException($"Cannot connect to ETS AddIn at {_host}:{_port}: {ex.Message}", ex);
        }

        _client = client;
        _lines = new JsonLineStream(client.GetStream());
    }

    public async Task<JsonNode?> SendAsync(JsonObject request, CancellationToken cancellationToken = default)
    {
        var lines = _lines ?? throw new TransportException("Transport not connected. Call ConnectAsync() first.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_readTimeout);
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
            throw new TransportException($"Connection to ETS AddIn failed: {ex.Message}", ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw new TransportException("Connection to ETS AddIn was closed", ex);
        }
    }

    /// <summary>Closes the socket and discards any partially read data.</summary>
    public Task CloseAsync()
    {
        _lines = null;
        _client?.Dispose();
        _client = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
