using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Port of test_tcp_transport.py: TcpTransport over a real loopback TCP server.
/// A local TCP server listens on an ephemeral port; incoming newline-delimited JSON
/// frames are dispatched through MockTransport.SendAsync, so the tests exercise the
/// real KNX bridge protocol over a real socket.
/// </summary>
public sealed class TcpTransportTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    // ---------------------------------------------------------------------------
    // Loopback server that delegates to MockTransport
    // ---------------------------------------------------------------------------

    /// <summary>TCP server that routes each line through a MockTransport instance.</summary>
    private sealed class LoopbackServer : IAsyncDisposable
    {
        private readonly SemaphoreSlim _mockLock = new(1, 1);
        private readonly object _gate = new();
        private readonly List<TcpClient> _clients = new();
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;

        public MockTransport Mock { get; } = new();

        public string Host => "127.0.0.1";

        /// <summary>Assigned after start.</summary>
        public int Port { get; private set; }

        public static async Task<LoopbackServer> StartAsync()
        {
            var server = new LoopbackServer();
            await server.Mock.ConnectAsync();
            server.Listen(0);
            return server;
        }

        /// <summary>Re-creates the listener on the same port (after <see cref="StopAsync"/>).</summary>
        public void Restart() => Listen(Port);

        private void Listen(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            // Allow re-binding the same port while closed connections linger in TIME_WAIT.
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Start();
            _listener = listener;
            // Grab the actual port the OS assigned.
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _cts = new CancellationTokenSource();
            _acceptLoop = AcceptLoopAsync(listener, _cts.Token);
        }

        /// <summary>Stops listening and forcibly closes all connected clients so their reads see EOF.</summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener = null;

            TcpClient[] clients;
            lock (_gate)
            {
                clients = _clients.ToArray();
                _clients.Clear();
            }
            foreach (var client in clients)
                client.Dispose();

            if (_acceptLoop is not null)
                await _acceptLoop;
            _acceptLoop = null;
            _cts?.Dispose();
            _cts = null;
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
                {
                    return;
                }

                lock (_gate)
                    _clients.Add(client);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false,
                                                    bufferSize: -1, leaveOpen: true);
                while (true)
                {
                    var raw = await reader.ReadLineAsync(cancellationToken);
                    if (raw is null)
                        break;
                    if (JsonNode.Parse(raw) is not JsonObject request)
                        break;

                    JsonNode? response;
                    await _mockLock.WaitAsync(cancellationToken);
                    try
                    {
                        response = await Mock.SendAsync(request);
                    }
                    finally
                    {
                        _mockLock.Release();
                    }

                    var line = Utf8NoBom.GetBytes((response?.ToJsonString() ?? "null") + "\n");
                    await stream.WriteAsync(line, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException
                                           or OperationCanceledException or InvalidOperationException)
            {
                // Connection dropped or server stopping.
            }
            finally
            {
                lock (_gate)
                    _clients.Remove(client);
                client.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _mockLock.Dispose();
        }
    }

    private static async Task<JsonObject> RequestOkAsync(KnxBridgeClient client, string method, JsonObject? parameters = null)
    {
        var result = await client.RequestAsync(method, parameters);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ObjB();
    }

    private static async Task<JsonArray> RequestArrayAsync(KnxBridgeClient client, string method, JsonObject? parameters = null)
    {
        var result = await client.RequestAsync(method, parameters);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ArrB();
    }

    /// <summary>tcp_client fixture: a KnxBridgeClient backed by a TcpTransport aimed at the loopback server
    /// (the C# client connects the transport lazily on its first request).</summary>
    private static KnxBridgeClient NewTcpClient(LoopbackServer loopback)
        => new(new TcpTransport(loopback.Host, loopback.Port, loopback.Mock.Token));

    // ---------------------------------------------------------------------------
    // Low-level transport tests (TestTcpTransportBasics)
    // ---------------------------------------------------------------------------

    // Transport connects and exposes the configured token.
    [Fact]
    public async Task TestConnectAndToken()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        await using var t = new TcpTransport(loopback.Host, loopback.Port, loopback.Mock.Token);
        await t.ConnectAsync();
        Assert.Equal("mock-token-for-testing", t.Token);
    }

    // Empty token = AddIn authentication disabled; the client must not force a token.
    // The AddIn ignores it when auth is off.
    [Fact]
    public void TestConstructorAllowsEmptyToken()
    {
        var t = new TcpTransport("127.0.0.1", 9999, token: "");
        Assert.Equal("", t.Token);
    }

    [Fact]
    public void TestConstructorRejectsInvalidPort()
    {
        var low = Assert.Throws<ArgumentOutOfRangeException>(() => new TcpTransport("127.0.0.1", 0, token: "tok"));
        Assert.Contains("Invalid port", low.Message);
        var high = Assert.Throws<ArgumentOutOfRangeException>(() => new TcpTransport("127.0.0.1", 70000, token: "tok"));
        Assert.Contains("Invalid port", high.Message);
    }

    [Fact]
    public async Task TestSendBeforeConnectRaises()
    {
        var t = new TcpTransport("127.0.0.1", 9999, token: "tok");
        var ex = await Assert.ThrowsAsync<TransportException>(() => t.SendAsync(new JsonObject
        {
            ["id"] = "1",
            ["token"] = "tok",
            ["method"] = "project.info",
        }));
        Assert.Contains("not connected", ex.Message);
    }

    // Connecting to a port with no listener raises a TransportException.
    [Fact]
    public async Task TestConnectRefused()
    {
        var t = new TcpTransport("127.0.0.1", 1, token: "tok");
        await Assert.ThrowsAsync<TransportException>(() => t.ConnectAsync());
    }

    // A single response line larger than a 64 KiB read buffer must still be read
    // (regression: LimitOverrunError on big ga.list).
    [Fact]
    public async Task TestLargeResponseLineOver64Kib()
    {
        var big = new string('x', 200_000); // one JSON line well over 64 KiB

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        async Task HandleAsync()
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false,
                                                bufferSize: -1, leaveOpen: true);
            await reader.ReadLineAsync();
            var resp = new JsonObject
            {
                ["id"] = "1",
                ["ok"] = true,
                ["result"] = new JsonObject { ["blob"] = big },
            };
            await stream.WriteAsync(Utf8NoBom.GetBytes(resp.ToJsonString() + "\n"));
            await stream.FlushAsync();
        }

        var server = HandleAsync();
        var t = new TcpTransport("127.0.0.1", port, token: "tok");
        await t.ConnectAsync();
        try
        {
            var resp = await t.SendAsync(new JsonObject
            {
                ["id"] = "1",
                ["token"] = "tok",
                ["method"] = "project.info",
                ["params"] = new JsonObject(),
            });
            Assert.Equal(big, resp?["result"]?["blob"].StrB());
        }
        finally
        {
            await t.CloseAsync();
            await server;
            listener.Stop();
        }
    }

    // ---------------------------------------------------------------------------
    // End-to-end protocol tests through the TCP loopback server (TestTcpProtocol)
    // ---------------------------------------------------------------------------

    // Read tool works end-to-end over TCP.
    [Fact]
    public async Task TestProjectInfo()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        await using var client = NewTcpClient(loopback);
        var info = await RequestOkAsync(client, "project.info");
        Assert.Equal("proj-001", info["projectId"].StrB());
        Assert.Equal("Test KNX Project", info["name"].StrB());
        Assert.Equal("ThreeLevel", info["groupAddressStyle"].StrB());
        Assert.True(info.HasB("revision"));
    }

    [Fact]
    public async Task TestListDevices()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        await using var client = NewTcpClient(loopback);
        var devices = await RequestArrayAsync(client, "devices.list");
        Assert.Equal(2, devices.Count);
        var refs = devices.Select(d => d.ObjB()["ref"].StrB()).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("dev-0001", refs);
        Assert.Contains("dev-0002", refs);
    }

    [Fact]
    public async Task TestListGroupAddresses()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        await using var client = NewTcpClient(loopback);
        var gas = await RequestArrayAsync(client, "ga.list");
        Assert.Equal(2, gas.Count);
        var names = gas.Select(ga => ga.ObjB()["name"].StrB()).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Light Living Room", names);
    }

    [Fact]
    public async Task TestListComobjects()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        await using var client = NewTcpClient(loopback);
        var cos = await RequestArrayAsync(client, "comobjects.list", new JsonObject { ["deviceRef"] = "dev-0001" });
        Assert.Equal(3, cos.Count);
        var co1 = cos.FindByRefB("co-0001");
        Assert.Contains("ga-0001", co1["links"].StringsB());
    }

    // A request with a wrong token gets an invalid_token error.
    [Fact]
    public async Task TestWrongTokenRejected()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        var t = new TcpTransport(loopback.Host, loopback.Port, token: "wrong-token");
        await t.ConnectAsync();
        try
        {
            var client = new KnxBridgeClient(t);
            var result = await client.RequestAsync("project.info");
            Assert.False(result.IsSuccess);
            Assert.Contains("invalid_token", result.Error!.ToString());
        }
        finally
        {
            await t.CloseAsync();
        }
    }

    // Mutations (ga.create) work and bump the project revision.
    [Fact]
    public async Task TestMutationOverTcp()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        await using var client = NewTcpClient(loopback);
        var infoBefore = await RequestOkAsync(client, "project.info");
        var revBefore = infoBefore["revision"].StrB();

        var result = await RequestOkAsync(client, "ga.create", new JsonObject
        {
            ["name"] = "Test GA",
            ["address"] = "2/0/1",
        });
        Assert.True(result.HasB("ref"));
        Assert.Equal("2/0/1", result["address"].StrB());

        var infoAfter = await RequestOkAsync(client, "project.info");
        Assert.NotEqual(revBefore, infoAfter["revision"].StrB());
    }

    // ---------------------------------------------------------------------------
    // Client-level reconnect-on-failure logic over TCP (TestTcpReconnect)
    // ---------------------------------------------------------------------------

    // After the server drops and restarts, the client reconnects once.
    [Fact]
    public async Task TestReconnectAfterServerRestart()
    {
        await using var loopback = await LoopbackServer.StartAsync();
        var t = new TcpTransport(
            loopback.Host,
            loopback.Port,
            loopback.Mock.Token,
            connectTimeout: TimeSpan.FromSeconds(2),
            readTimeout: TimeSpan.FromSeconds(2));
        await t.ConnectAsync();
        await using var client = new KnxBridgeClient(t);

        // Sanity: first call succeeds.
        var info = await RequestOkAsync(client, "project.info");
        Assert.Equal("proj-001", info["projectId"].StrB());

        // Restart the server (same port).
        var port = loopback.Port;
        await loopback.StopAsync();
        loopback.Restart();
        Assert.Equal(port, loopback.Port);

        // The next call should trigger one reconnect and succeed.
        var info2 = await RequestOkAsync(client, "project.info");
        Assert.Equal("proj-001", info2["projectId"].StrB());

        await t.CloseAsync();
    }
}
