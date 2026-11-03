using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Idempotency-key handling (port of tests/test_idempotency.py).
/// Idempotency keys are fresh UUIDs per operation (not a hash of method+params): two
/// separate tool calls with identical params get DIFFERENT keys and execute
/// independently, so sequences like link -> unlink -> link work correctly.
/// </summary>
public sealed class IdempotencyTests
{
    [Fact]
    public async Task DifferentParamsDifferentResult()
    {
        // Different params produce distinct resources (sanity check).
        await using var host = await McpTestHost.StartAsync();
        var d1 = ToolAssert.Object(await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "GA One"),
            ("address", "3/0/1"))));
        var d2 = ToolAssert.Object(await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "GA Two"),
            ("address", "3/0/2"))));
        Assert.NotEqual(d1.StringAt("ref"), d2.StringAt("ref"));
        Assert.NotEqual(d1.StringAt("address"), d2.StringAt("address"));
    }

    [Fact]
    public async Task IdempotencyCacheIsPerTransport()
    {
        // Two transports (= two AddIn instances) have independent caches.
        var t1 = new MockTransport();
        await t1.ConnectAsync();
        var t2 = new MockTransport();
        await t2.ConnectAsync();

        // Test with transport 1
        JsonObject d1;
        await using (var host1 = await McpTestHost.StartAsync(t1))
        {
            d1 = ToolAssert.Object(await host1.CallAsync("knx_create_group_address", Args.Of(
                ("name", "Shared GA"),
                ("address", "4/0/1"))));
        }

        // Test with transport 2
        JsonObject d2;
        await using (var host2 = await McpTestHost.StartAsync(t2))
        {
            d2 = ToolAssert.Object(await host2.CallAsync("knx_create_group_address", Args.Of(
                ("name", "Shared GA"),
                ("address", "4/0/1"))));
        }

        // Different transports -> different refs
        Assert.NotEqual(d1.StringAt("ref"), d2.StringAt("ref"));
    }

    [Fact]
    public async Task LinkUnlinkLinkExecutesBothLinks()
    {
        // State-transition test: link -> unlink -> link must execute both links.
        // With fresh-UUID idempotency keys, the second link gets a different key than
        // the first and is therefore NOT deduplicated (broken with sha256(method+params) keys).
        await using var host = await McpTestHost.StartAsync();

        // 1. Link
        var r1 = await host.CallAsync("knx_link", LinkArgs());
        Assert.False(r1.IsError == true);
        Assert.Equal("co-0002", ToolAssert.Object(r1).StringAt("comObjectRef"));

        // Verify link exists
        Assert.Contains("ga-0002", await Co2LinksAsync(host));

        // 2. Unlink
        var r2 = await host.CallAsync("knx_unlink", LinkArgs());
        Assert.False(r2.IsError == true);
        Assert.True(ToolAssert.Object(r2).BoolAt("ok"));

        // Verify link is gone
        Assert.DoesNotContain("ga-0002", await Co2LinksAsync(host));

        // 3. Re-link (must succeed, not be deduplicated)
        var r3 = await host.CallAsync("knx_link", LinkArgs());
        Assert.False(r3.IsError == true);
        Assert.Equal("co-0002", ToolAssert.Object(r3).StringAt("comObjectRef"));

        // Verify link is back
        Assert.Contains("ga-0002", await Co2LinksAsync(host));
    }

    private static IReadOnlyDictionary<string, object?> LinkArgs()
        => Args.Of(("com_object_ref", "co-0002"), ("ga_ref", "ga-0002"));

    private static async Task<List<string>> Co2LinksAsync(McpTestHost host)
    {
        var cos = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        return cos.FindBy("ref", "co-0002").ArrayAt("links").Strings();
    }

    [Fact]
    public async Task IdenticalGaCreatesAreDistinct()
    {
        // Two identical ga.create calls get different keys and both execute. The second
        // fails on the duplicate-address check, proving it was NOT deduplicated.
        await using var host = await McpTestHost.StartAsync();
        var d1 = ToolAssert.Object(await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Same GA"),
            ("address", "2/0/1"))));
        Assert.True(d1.ContainsKey("ref"));

        // Second identical call: hits duplicate-address check (not idempotency cache)
        var r2 = await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Same GA"),
            ("address", "2/0/1")));
        var error = ToolAssert.ErrorText(r2);
        Assert.Contains("invalid_params", error);
        Assert.Contains("already exists", error);
    }

    [Fact]
    public async Task IdempotencyReplayBeforeRevisionCheck()
    {
        // Idempotency cache replay must happen BEFORE the revision check: a mutation
        // succeeds but the response is lost; the client retries with the same
        // idempotencyKey AND the original (now stale) expectedProjectRevision. The
        // AddIn must return the cached success, not revision_mismatch.
        await using var transport = new MockTransport();
        await transport.ConnectAsync();

        var idemKey = Guid.NewGuid().ToString();

        // First call: succeeds, bumps revision from rev-001 to rev-002.
        var req1 = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["token"] = transport.Token,
            ["method"] = "ga.create",
            ["params"] = new JsonObject { ["name"] = "Replay GA", ["address"] = "6/0/1" },
            ["idempotencyKey"] = idemKey,
            ["expectedProjectRevision"] = "rev-001",
        };
        var resp1 = await transport.SendAsync(req1);
        Assert.True(resp1.BoolAt("ok"));
        Assert.Equal("rev-002", resp1.StringAt("projectRevision"));

        // Retry with a new request id but same idempotencyKey and old revision.
        var req2 = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["token"] = transport.Token,
            ["method"] = "ga.create",
            ["params"] = new JsonObject { ["name"] = "Replay GA", ["address"] = "6/0/1" },
            ["idempotencyKey"] = idemKey,
            ["expectedProjectRevision"] = "rev-001",  // stale!
        };
        var resp2 = await transport.SendAsync(req2);
        // Must be a cache hit (success), NOT revision_mismatch.
        Assert.True(resp2.BoolAt("ok"));
        Assert.True(
            JsonNode.DeepEquals(resp1.NodeAt("result"), resp2.NodeAt("result")),
            $"Replayed result differs: {resp1.NodeAt("result")?.ToJsonString()} vs {resp2.NodeAt("result")?.ToJsonString()}");
    }
}
