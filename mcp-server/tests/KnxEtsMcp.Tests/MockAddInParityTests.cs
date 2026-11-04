using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Envelope checks where the mock must answer exactly like the AddIn's IpcServer
/// (same error codes and messages), so client behaviour tested against the mock holds
/// against real ETS.
/// </summary>
public sealed class MockAddInParityTests
{
    private static async Task<JsonNode?> SendAsync(MockTransport transport, JsonObject request)
    {
        request["id"] = "req-1";
        request["params"] ??= new JsonObject();
        return await transport.SendAsync(request);
    }

    private static void AssertError(JsonNode? response, string code, string message)
    {
        Assert.False(response.BoolAt("ok"));
        Assert.Equal(code, response.NodeAt("error").StringAt("code"));
        Assert.Equal(message, response.NodeAt("error").StringAt("message"));
    }

    [Fact]
    public async Task MissingToken_IsInvalidParams()
    {
        await using var transport = new MockTransport();
        await transport.ConnectAsync();

        var response = await SendAsync(transport, new JsonObject { ["method"] = "project.info" });

        AssertError(response, "invalid_params", "Missing 'token' field.");
    }

    [Fact]
    public async Task WrongToken_IsAuthFailed()
    {
        await using var transport = new MockTransport();
        await transport.ConnectAsync();

        var response = await SendAsync(transport, new JsonObject { ["token"] = "wrong", ["method"] = "project.info" });

        AssertError(response, "auth_failed", "Invalid session token.");
    }

    [Fact]
    public async Task MutatingMethodWithoutIdempotencyKey_IsInvalidParams()
    {
        await using var transport = new MockTransport();
        await transport.ConnectAsync();

        var response = await SendAsync(transport, new JsonObject
        {
            ["token"] = transport.Token,
            ["method"] = "ga.create",
            ["params"] = new JsonObject { ["name"] = "No key", ["address"] = "7/0/1" },
        });

        AssertError(response, "invalid_params", "idempotencyKey is required for mutating methods.");
    }

    [Fact]
    public async Task ReadWithoutIdempotencyKey_IsAllowed()
    {
        await using var transport = new MockTransport();
        await transport.ConnectAsync();

        var response = await SendAsync(transport, new JsonObject { ["token"] = transport.Token, ["method"] = "project.info" });

        Assert.True(response.BoolAt("ok"));
    }

    [Fact]
    public async Task BusReadOnly_RefusesReplayOfAnEarlierBusWrite()
    {
        // The bus gate runs before the idempotency replay (AddIn order): enabling
        // "Bus read-only" also refuses a retried bus write that already ran once.
        await using var transport = new MockTransport();
        await transport.ConnectAsync();
        JsonObject GroupWrite() => new()
        {
            ["token"] = transport.Token,
            ["method"] = "group.write",
            ["params"] = new JsonObject { ["gaRef"] = "ga-0001", ["value"] = "01" },
            ["idempotencyKey"] = "same-key",
        };
        var first = await SendAsync(transport, GroupWrite());
        Assert.True(first.BoolAt("ok"));

        transport.BusReadOnly = true;
        var replay = await SendAsync(transport, GroupWrite());

        Assert.False(replay.BoolAt("ok"));
        Assert.Equal("approval_required", replay.NodeAt("error").StringAt("code"));
    }
}
