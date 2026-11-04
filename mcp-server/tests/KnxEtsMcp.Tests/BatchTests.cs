using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Port of test_batch.py: batch.apply -- many project mutations in one atomic undo marker.
/// Covers success, atomic rollback on failure, best-effort (atomic=false), and
/// validation (empty, non-batchable method, unknown method, nested batch).
/// </summary>
public sealed class BatchTests
{
    private static KnxBridgeClient NewBridge() => new(new MockTransport());

    /// <summary>One batch operation: {"method": ..., "params": {...}} (string params).</summary>
    private static JsonObject Op(string method, params (string Name, string Value)[] parameters)
    {
        var p = new JsonObject();
        foreach (var (name, value) in parameters)
            p[name] = value;
        return new JsonObject { ["method"] = method, ["params"] = p };
    }

    private static JsonArray Ops(params JsonObject[] operations)
    {
        var array = new JsonArray();
        foreach (var operation in operations)
            array.Add((JsonNode)operation);
        return array;
    }

    private static Task<Result<JsonNode?>> BatchApplyAsync(
        KnxBridgeClient bridge, JsonArray operations, bool atomic = true, bool validateOnly = false)
        => bridge.RequestAsync("batch.apply", new JsonObject
        {
            ["operations"] = operations,
            ["atomic"] = atomic,
            ["validateOnly"] = validateOnly,
        });

    private static async Task<JsonObject> BatchApplyOkAsync(
        KnxBridgeClient bridge, JsonArray operations, bool atomic = true, bool validateOnly = false)
    {
        var result = await BatchApplyAsync(bridge, operations, atomic, validateOnly);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ObjB();
    }

    /// <summary>Expects the call to fail and returns "[code] message".</summary>
    private static async Task<string> BatchApplyErrorAsync(KnxBridgeClient bridge, JsonArray operations)
    {
        var result = await BatchApplyAsync(bridge, operations);
        Assert.False(result.IsSuccess, "expected error");
        return result.Error!.ToString();
    }

    private static async Task<JsonArray> ListGroupAddressesAsync(KnxBridgeClient bridge)
    {
        var result = await bridge.RequestAsync("ga.list");
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ArrB();
    }

    private static async Task<int> GaCountAsync(KnxBridgeClient bridge)
        => (await ListGroupAddressesAsync(bridge)).Count;

    private static async Task<JsonObject> RequestOkAsync(KnxBridgeClient bridge, string method, JsonObject parameters)
    {
        var result = await bridge.RequestAsync(method, parameters);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ObjB();
    }

    private static List<string> Statuses(JsonObject res)
        => res["results"].ObjectsB().Select(r => r["status"].StrB()).ToList();

    [Fact]
    public async Task TestBatchSuccessAtomic()
    {
        await using var bridge = NewBridge();
        var before = await GaCountAsync(bridge);
        var res = await BatchApplyOkAsync(bridge, Ops(
            Op("ga.create", ("name", "Batch A"), ("address", "6/0/1")),
            Op("ga.create", ("name", "Batch B"), ("address", "6/0/2"))));
        res["applied"].IsTrueB();
        res["atomic"].IsTrueB();
        res["rolledBack"].IsFalseB();
        Assert.Equal(2, res["total"].IntB());
        Assert.Equal(2, res["ok"].IntB());
        Assert.Equal(0, res["failed"].IntB());
        Assert.Equal(0, res["skipped"].IntB());
        Assert.Equal(new[] { "ok", "ok" }, Statuses(res));
        Assert.Equal(before + 2, await GaCountAsync(bridge));
    }

    [Fact]
    public async Task TestBatchAtomicRollbackOnFailure()
    {
        await using var bridge = NewBridge();
        var before = await GaCountAsync(bridge);
        var res = await BatchApplyOkAsync(bridge, Ops(
            Op("ga.create", ("name", "Rollback A"), ("address", "6/1/1")),
            Op("ga.rename", ("groupAddressRef", "does-not-exist"), ("name", "X")),
            Op("ga.create", ("name", "Never"), ("address", "6/1/9"))));
        res["applied"].IsFalseB();
        res["rolledBack"].IsTrueB();
        Assert.Equal(1, res["ok"].IntB());
        Assert.Equal(1, res["failed"].IntB());
        Assert.Equal(1, res["skipped"].IntB());
        Assert.Equal(new[] { "ok", "error", "skipped" }, Statuses(res));
        Assert.Equal("not_found", res["results"].ArrB()[1]?["error"]?["code"].StrB());
        // Rollback: the GA created in step 0 must be gone, and step 2 never ran.
        Assert.Equal(before, await GaCountAsync(bridge));
        var names = (await ListGroupAddressesAsync(bridge)).Select(g => g.ObjB()["name"].StrB()).ToList();
        Assert.DoesNotContain("Rollback A", names);
        Assert.DoesNotContain("Never", names);
    }

    [Fact]
    public async Task TestBatchContinueOnError()
    {
        await using var bridge = NewBridge();
        var before = await GaCountAsync(bridge);
        var res = await BatchApplyOkAsync(bridge, Ops(
            Op("ga.create", ("name", "Best A"), ("address", "6/2/1")),
            Op("ga.rename", ("groupAddressRef", "nope"), ("name", "X")),
            Op("ga.create", ("name", "Best B"), ("address", "6/2/2"))), atomic: false);
        res["applied"].IsTrueB();
        res["rolledBack"].IsFalseB();
        Assert.Equal(2, res["ok"].IntB());
        Assert.Equal(1, res["failed"].IntB());
        Assert.Equal(0, res["skipped"].IntB());
        Assert.Equal(new[] { "ok", "error", "ok" }, Statuses(res));
        // Both valid steps persisted despite the failure in the middle.
        Assert.Equal(before + 2, await GaCountAsync(bridge));
    }

    [Fact]
    public async Task TestBatchRejectsEmpty()
    {
        await using var bridge = NewBridge();
        var error = await BatchApplyErrorAsync(bridge, Ops());
        Assert.Contains("operations", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestBatchRejectsNonBatchableMethod()
    {
        await using var bridge = NewBridge();
        var error = await BatchApplyErrorAsync(bridge, Ops(Op("device.program", ("deviceRef", "d1"))));
        Assert.Contains("not allowed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestBatchRejectsNestedBatch()
    {
        await using var bridge = NewBridge();
        var nested = new JsonObject
        {
            ["method"] = "batch.apply",
            ["params"] = new JsonObject { ["operations"] = new JsonArray() },
        };
        var error = await BatchApplyErrorAsync(bridge, Ops(nested));
        Assert.Contains("not allowed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestBatchRejectsUnknownMethod()
    {
        await using var bridge = NewBridge();
        var error = await BatchApplyErrorAsync(bridge, Ops(Op("foo.bar")));
        Assert.Contains("not allowed", error, StringComparison.OrdinalIgnoreCase);
    }

    // End-to-end through the MCP tool layer.
    [Fact]
    public async Task TestBatchViaMcpTool()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_batch_apply", Args.Of(
            ("operations", Ops(
                Op("ga.create", ("name", "Tool A"), ("address", "6/3/1")),
                Op("ga.create", ("name", "Tool B"), ("address", "6/3/2")))))));
        data["applied"].IsTrueB();
        Assert.Equal(2, data["ok"].IntB());
    }

    // Create a GA, then rename it to a name longer than the mock's limit.
    [Fact]
    public async Task TestRenameReportsTruncation()
    {
        await using var bridge = NewBridge();
        var ga = await RequestOkAsync(bridge, "ga.create", new JsonObject { ["name"] = "Short", ["address"] = "6/5/1" });
        var longName = new string('X', 80);
        var res = await RequestOkAsync(bridge, "ga.rename", new JsonObject
        {
            ["groupAddressRef"] = ga["ref"].StrB(),
            ["name"] = longName,
        });
        res["truncated"].IsTrueB();
        Assert.True(res["name"].StrB().Length < longName.Length);
    }

    [Fact]
    public async Task TestRenameNoTruncationFlagWhenWithinLimit()
    {
        await using var bridge = NewBridge();
        var ga = await RequestOkAsync(bridge, "ga.create", new JsonObject { ["name"] = "Short2", ["address"] = "6/5/2" });
        var res = await RequestOkAsync(bridge, "ga.rename", new JsonObject
        {
            ["groupAddressRef"] = ga["ref"].StrB(),
            ["name"] = "Still short",
        });
        Assert.False(res.HasB("truncated")); // omitted when not truncated
    }

    [Fact]
    public async Task TestBatchViaMcpToolRollback()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_batch_apply", Args.Of(
            ("operations", Ops(
                Op("ga.create", ("name", "T Roll"), ("address", "6/4/1")),
                Op("ga.rename", ("groupAddressRef", "missing"), ("name", "X")))))));
        data["applied"].IsFalseB();
        data["rolledBack"].IsTrueB();
        var results = data["results"].ObjectsB();
        Assert.Equal("ok", results[0]["status"].StrB());
        Assert.Equal("error", results[1]["status"].StrB());
    }

    // -- operations as a JSON string (untyped-schema client behaviour) -------------
    // Some MCP clients serialise the operations array as a JSON *string* for an untyped
    // parameter. The tool must accept that form, not reject it as "not an array".

    [Fact]
    public async Task TestBatchAcceptsOperationsAsJsonString()
    {
        await using var host = await McpTestHost.StartAsync();
        var opsJson = Ops(
            Op("ga.create", ("name", "Str A"), ("address", "9/0/1")),
            Op("ga.create", ("name", "Str B"), ("address", "9/0/2"))).ToJsonString();
        var data = ToolAssert.Object(await host.CallAsync("knx_batch_apply", Args.Of(("operations", opsJson))));
        data["applied"].IsTrueB();
        Assert.Equal(2, data["ok"].IntB());
    }

    [Fact]
    public async Task TestBatchStringEncodedRollback()
    {
        await using var host = await McpTestHost.StartAsync();
        // Second op duplicates seed address 1/0/1 -> fails -> atomic rollback, even via the string form.
        var opsJson = Ops(
            Op("ga.create", ("name", "SR A"), ("address", "9/1/1")),
            Op("ga.create", ("name", "Dup"), ("address", "1/0/1"))).ToJsonString();
        var data = ToolAssert.Object(await host.CallAsync("knx_batch_apply", Args.Of(("operations", opsJson))));
        data["applied"].IsFalseB();
        data["rolledBack"].IsTrueB();
    }

    [Fact]
    public async Task TestBatchRejectsNonArrayOperationsWithTypedError()
    {
        await using var host = await McpTestHost.StartAsync();
        // A JSON object instead of an array: the error must be invalid_params and name the type.
        var error = await ToolAssert.FailureAsync(() => host.CallAsync("knx_batch_apply",
            Args.Of(("operations", new JsonObject { ["method"] = "ga.create" }))));
        Assert.Contains("invalid_params", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestBatchRejectsStringThatIsNotValidJson()
    {
        await using var host = await McpTestHost.StartAsync();
        var error = await ToolAssert.FailureAsync(() => host.CallAsync("knx_batch_apply",
            Args.Of(("operations", "not json at all"))));
        Assert.Contains("invalid_params", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeOperationsUnit()
    {
        // A real array passes through.
        var arr = KnxEtsMcp.Application.Tools.BatchTools.NormalizeOperations(
            ArrayElement(Op("ga.create", ("name", "A"), ("address", "1/2/3"))));
        Assert.Single(arr);

        // A string-encoded array is unwrapped.
        var fromString = KnxEtsMcp.Application.Tools.BatchTools.NormalizeOperations(
            StringElement(Ops(Op("ga.create", ("name", "A"), ("address", "1/2/3"))).ToJsonString()));
        Assert.Single(fromString);

        // An empty array is rejected.
        Assert.Throws<ModelContextProtocol.McpException>(
            () => KnxEtsMcp.Application.Tools.BatchTools.NormalizeOperations(ArrayElement()));

        // A JSON number is rejected with a message that names the type.
        var ex = Assert.Throws<ModelContextProtocol.McpException>(
            () => KnxEtsMcp.Application.Tools.BatchTools.NormalizeOperations(NumberElement(5)));
        Assert.Contains("number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static System.Text.Json.JsonElement ArrayElement(params JsonObject[] operations)
        => ToElement(Ops(operations));

    private static System.Text.Json.JsonElement StringElement(string value)
        => ToElement(JsonValue.Create(value));

    private static System.Text.Json.JsonElement NumberElement(int value)
        => ToElement(JsonValue.Create(value));

    private static System.Text.Json.JsonElement ToElement(JsonNode? node)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(node?.ToJsonString() ?? "null");
        return doc.RootElement.Clone();
    }

    // -- validate_only: read-only pre-flight ---------------------------------------

    [Fact]
    public async Task TestBatchValidateOnlyAllValid()
    {
        await using var bridge = NewBridge();
        var before = await GaCountAsync(bridge);
        var res = await BatchApplyOkAsync(bridge, Ops(
            Op("ga.create", ("name", "Pre A"), ("address", "7/0/9")),
            Op("link.create", ("comObjectRef", "co-0002"), ("gaRef", "ga-0001"))), validateOnly: true);
        res["validated"].IsTrueB();
        Assert.Equal(2, res["total"].IntB());
        Assert.Equal(2, res["valid"].IntB());
        Assert.Equal(0, res["invalid"].IntB());
        Assert.All(res["results"].ObjectsB(), r =>
        {
            r["valid"].IsTrueB();
            Assert.Empty(r["issues"].ArrB());
        });
        // Nothing was mutated.
        Assert.Equal(before, await GaCountAsync(bridge));
    }

    [Fact]
    public async Task TestBatchValidateOnlyReportsIssues()
    {
        await using var bridge = NewBridge();
        var before = await GaCountAsync(bridge);
        var res = await BatchApplyOkAsync(bridge, Ops(
            Op("link.create", ("comObjectRef", "co-9999"), ("gaRef", "ga-0001")),
            Op("link.create", ("comObjectRef", "co-0003"), ("gaRef", "ga-0001")),
            Op("ga.create", ("name", "Dup"), ("address", "1/0/1")),
            Op("link.create", ("comObjectRef", "co-0004"), ("gaRef", "ga-0001")),
            Op("ga.create", ("name", "NoAddr"))), validateOnly: true);
        res["validated"].IsTrueB();
        Assert.Equal(5, res["total"].IntB());
        Assert.Equal(0, res["valid"].IntB());
        Assert.Equal(5, res["invalid"].IntB());
        var r = res["results"].ObjectsB();
        string Issues(int i) => string.Join(" ", r[i]["issues"].StringsB());
        Assert.Contains("not found", Issues(0), StringComparison.OrdinalIgnoreCase);                  // co-9999 missing
        Assert.Contains("inactive", Issues(1), StringComparison.OrdinalIgnoreCase);                   // co-0003 inactive
        Assert.Contains("already exists", Issues(2), StringComparison.OrdinalIgnoreCase);             // 1/0/1 collision
        Assert.Contains("mismatch", Issues(3), StringComparison.OrdinalIgnoreCase);                   // 5.001 vs 1.001
        Assert.Contains("missing required parameter", Issues(4), StringComparison.OrdinalIgnoreCase); // no address
        // Read-only: no GA created despite the ga.create ops.
        Assert.Equal(before, await GaCountAsync(bridge));
    }
}
