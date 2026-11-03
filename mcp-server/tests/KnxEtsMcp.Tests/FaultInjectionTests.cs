using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Port of test_fault_injection.py: transport failures (timeout, EOF, malformed
/// responses, id mismatches) and the client's retry and validation behaviour.
/// Differences from Python: the C# client connects lazily, so ConnectAsync runs once
/// for the first request and once more for each reconnect; transport failures are
/// signalled with <see cref="TransportException"/>.
/// </summary>
public sealed class FaultInjectionTests
{
    /// <summary>Transport whose responses and failures are scripted by the test; counts calls.</summary>
    private sealed class FakeTransport : ITransport
    {
        public string Token => "fake-token";

        /// <summary>Produces the response for a request (or throws to simulate a failure).</summary>
        public Func<JsonObject, JsonNode?> OnSend { get; set; }
            = _ => throw new InvalidOperationException("FakeTransport: no response configured");

        /// <summary>Given the 1-based connect attempt number, the exception to throw (null = succeed).</summary>
        public Func<int, Exception?> OnConnect { get; set; } = _ => null;

        public int ConnectCalls { get; private set; }

        public int SendCalls { get; private set; }

        public int CloseCalls { get; private set; }

        public List<JsonObject> Requests { get; } = new();

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            var failure = OnConnect(ConnectCalls);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }

        public Task<JsonNode?> SendAsync(JsonObject request, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            Requests.Add(request);
            try
            {
                return Task.FromResult(OnSend(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<JsonNode?>(ex);
            }
        }

        public Task CloseAsync()
        {
            CloseCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static string IdOf(JsonObject request) => request["id"].StrB();

    private static Task<Result<JsonNode?>> ProjectInfoAsync(KnxBridgeClient client)
        => client.RequestAsync("project.info");

    private static void AssertFailure(Result<JsonNode?> result, string messagePart)
    {
        Assert.False(result.IsSuccess, "expected a bridge error");
        Assert.NotNull(result.Error);
        Assert.Contains(messagePart, result.Error.Message);
    }

    // -- Malformed response --------------------------------------------------------

    // A response that is not a JSON object must be rejected.
    [Fact]
    public async Task TestMalformedResponseNotDict()
    {
        var t = new FakeTransport { OnSend = _ => JsonValue.Create("this is not a dict") };
        var client = new KnxBridgeClient(t);

        AssertFailure(await ProjectInfoAsync(client), "not a JSON object");
    }

    // A response with a mismatched id must be rejected.
    [Fact]
    public async Task TestResponseIdMismatch()
    {
        var t = new FakeTransport
        {
            OnSend = _ => new JsonObject
            {
                ["id"] = "wrong-id-not-matching",
                ["ok"] = true,
                ["result"] = new JsonObject { ["projectId"] = "p1" },
                ["projectRevision"] = "rev-001",
            },
        };
        var client = new KnxBridgeClient(t);

        AssertFailure(await ProjectInfoAsync(client), "id mismatch");
    }

    // A success response (ok=true) without 'result' must be rejected.
    [Fact]
    public async Task TestSuccessResponseMissingResult()
    {
        var t = new FakeTransport
        {
            OnSend = request => new JsonObject
            {
                ["id"] = IdOf(request),
                ["ok"] = true,
                // "result" is missing
                ["projectRevision"] = "rev-001",
            },
        };
        var client = new KnxBridgeClient(t);

        AssertFailure(await ProjectInfoAsync(client), "missing result");
    }

    // An error response (ok=false) without 'error' must be rejected.
    [Fact]
    public async Task TestErrorResponseMissingError()
    {
        var t = new FakeTransport
        {
            OnSend = request => new JsonObject
            {
                ["id"] = IdOf(request),
                ["ok"] = false,
                // "error" is missing
            },
        };
        var client = new KnxBridgeClient(t);

        AssertFailure(await ProjectInfoAsync(client), "missing error");
    }

    // -- Transport failure and retry -----------------------------------------------

    // Client retries once on transport failure with reconnect.
    [Fact]
    public async Task TestRetryOnTransportFailure()
    {
        var callCount = 0;
        var t = new FakeTransport
        {
            OnSend = request =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TransportException("pipe broken");
                return new JsonObject
                {
                    ["id"] = IdOf(request),
                    ["ok"] = true,
                    ["result"] = new JsonObject
                    {
                        ["projectId"] = "proj-001",
                        ["name"] = "Test",
                        ["groupAddressStyle"] = "ThreeLevel",
                        ["revision"] = "rev-001",
                    },
                    ["projectRevision"] = "rev-001",
                };
            },
        };
        var client = new KnxBridgeClient(t);

        var result = await ProjectInfoAsync(client);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal("proj-001", result.Value?["projectId"].StrB());
        Assert.Equal(2, callCount);
        // ConnectAsync ran for the lazy initial connect plus once for the reconnect
        // (Python asserted only the reconnect: connect.assert_called_once()).
        Assert.Equal(2, t.ConnectCalls);
    }

    // If both the original send and the retry fail, a bridge error is returned.
    [Fact]
    public async Task TestRetryExhaustedRaises()
    {
        var t = new FakeTransport { OnSend = _ => throw new TransportException("pipe permanently broken") };
        var client = new KnxBridgeClient(t);

        AssertFailure(await ProjectInfoAsync(client), "Transport failed after reconnect");
    }

    // On transport retry, the same idempotency key is reused.
    [Fact]
    public async Task TestRetryReusesIdempotencyKey()
    {
        var t = new FakeTransport();
        t.OnSend = request =>
        {
            if (t.Requests.Count == 1)
                throw new TransportException("transient failure");
            return new JsonObject
            {
                ["id"] = IdOf(request),
                ["ok"] = true,
                ["result"] = new JsonObject { ["ref"] = "ga-new", ["address"] = "7/0/1" },
                ["projectRevision"] = "rev-002",
            };
        };
        var client = new KnxBridgeClient(t);

        var result = await client.RequestAsync("ga.create", new JsonObject
        {
            ["name"] = "Test GA",
            ["address"] = "7/0/1",
        });
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal("ga-new", result.Value?["ref"].StrB());

        // Both attempts used the same request (same id and idempotency key)
        Assert.Equal(2, t.Requests.Count);
        Assert.Equal(IdOf(t.Requests[0]), IdOf(t.Requests[1]));
        Assert.Equal(t.Requests[0]["idempotencyKey"].StrB(), t.Requests[1]["idempotencyKey"].StrB());
    }

    // If reconnect itself fails, the reconnect failure surfaces.
    [Fact]
    public async Task TestReconnectFailureRaises()
    {
        var t = new FakeTransport
        {
            OnSend = _ => throw new TransportException("pipe broken"),
            // The first (lazy) connect succeeds; the reconnect after the failed send fails.
            OnConnect = attempt => attempt >= 2 ? new TransportException("cannot reconnect") : null,
        };
        var client = new KnxBridgeClient(t);

        AssertFailure(await ProjectInfoAsync(client), "reconnect unsuccessful");
    }
}
