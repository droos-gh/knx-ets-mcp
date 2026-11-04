using System.Text.Json;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Port of test_recover.py: read methods bridge.info, bus.reconstructLine, device.readGroupObjects.</summary>
public sealed class RecoverTests
{
    // -- knx_bridge_info (bridge.info) --------------------------------------------

    // bridge.info returns AddIn/SDK version, project metadata, and knxIpOnly.
    [Fact]
    public async Task TestBridgeInfo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bridge_info"));
        Assert.Equal("0.4.1", data["addinVersion"].StrB());
        Assert.Equal("6.3.7959.0", data["sdkVersion"].StrB());
        Assert.Equal("6.4.8658.0", data["builtAgainstSdk"].StrB());
        JsonTestExtensionsB.AssertKindB(data["projectName"], JsonValueKind.String);
        JsonTestExtensionsB.AssertKindB(data["projectId"], JsonValueKind.String);
        JsonTestExtensionsB.AssertKindB(data["projectRevision"], JsonValueKind.String);
        data["knxIpOnly"].IsTrueB();
    }

    // bridge.info response contains exactly the expected keys.
    [Fact]
    public async Task TestBridgeInfoHasAllKeys()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bridge_info"));
        var expectedKeys = new[]
        {
            "addinVersion", "sdkVersion", "builtAgainstSdk", "projectName",
            "projectId", "projectRevision", "knxIpOnly",
        };
        Assert.Equal(
            expectedKeys.Order(StringComparer.Ordinal).ToArray(),
            data.Select(p => p.Key).Order(StringComparer.Ordinal).ToArray());
    }

    // bridge.info is a read -- it must NOT bump the revision.
    [Fact]
    public async Task TestBridgeInfoIsRead()
    {
        await using var host = await McpTestHost.StartAsync();
        var revBefore = host.Mock.Revision;
        await host.CallAsync("knx_bridge_info");
        Assert.Equal(revBefore, host.Mock.Revision);
    }

    // -- knx_reconstruct_line (bus.reconstructLine) --------------------------------

    // bus.reconstructLine returns a jobId; job.status completes with devices.
    [Fact]
    public async Task TestReconstructLine()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_reconstruct_line", Args.Of(("line_ref", "line-1.1"))));
        Assert.True(data.HasB("jobId"));
        var jobId = data["jobId"].StrB();

        // Poll job status
        var status = ToolAssert.Object(await host.CallAsync("knx_job_status", Args.Of(("job_id", jobId))));
        Assert.Equal("done", status["state"].StrB());
        Assert.True(status.HasB("result"));

        var jobResult = status["result"].ObjB();
        var devices = jobResult["devices"].ArrB();
        Assert.NotEmpty(devices);
        JsonTestExtensionsB.AssertKindB(jobResult["scannedRange"], JsonValueKind.String);

        // Check device shape
        var dev = devices[0];
        Assert.True(dev.HasB("address"));
        Assert.True(dev.HasB("maskVersion"));
        Assert.True(dev.HasB("serialNumber"));
        Assert.True(dev.HasB("manufacturerId"));
        Assert.True(dev.HasB("error")); // present, may be None
    }

    // bus.reconstructLine with unknown lineRef returns not_found.
    [Fact]
    public async Task TestReconstructLineNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_reconstruct_line", Args.Of(("line_ref", "line-9.9")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // bus.reconstructLine is a read -- it must NOT bump the revision.
    [Fact]
    public async Task TestReconstructLineIsRead()
    {
        await using var host = await McpTestHost.StartAsync();
        var revBefore = host.Mock.Revision;
        await host.CallAsync("knx_reconstruct_line", Args.Of(("line_ref", "line-1.1")));
        Assert.Equal(revBefore, host.Mock.Revision);
    }

    // -- knx_read_group_objects (device.readGroupObjects) --------------------------

    // device.readGroupObjects returns comObjects with flags and partial=true.
    [Fact]
    public async Task TestReadGroupObjects()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_read_group_objects", Args.Of(("address", "1.1.1"))));
        Assert.Equal("1.1.1", data["address"].StrB());
        var comObjects = data["comObjects"].ObjectsB();
        Assert.NotEmpty(comObjects);
        data["partial"].IsTrueB();
        JsonTestExtensionsB.AssertKindB(data["note"], JsonValueKind.String);

        // Check comObject shape
        var co = comObjects[0];
        Assert.True(co.HasB("number"));
        co["groupAddresses"].ArrB();
        Assert.True(co.HasB("flags"));
        var flags = co["flags"].ObjB();
        foreach (var key in new[] { "c", "r", "w", "t", "u" })
        {
            Assert.True(flags.HasB(key), key);
            flags[key].IsBoolB();
        }
    }

    // device.readGroupObjects includes linked group addresses.
    [Fact]
    public async Task TestReadGroupObjectsHasLinkedGas()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_read_group_objects", Args.Of(("address", "1.1.1"))));
        // Device dev-0001 at 1.1.1 has co-0001 linked to ga-0001 (address "1/0/1")
        var coWithLinks = data["comObjects"].ObjectsB()
            .Where(co => co["groupAddresses"].ArrB().Count > 0)
            .ToList();
        Assert.NotEmpty(coWithLinks);
        Assert.Contains("1/0/1", coWithLinks[0]["groupAddresses"].StringsB());
    }

    // device.readGroupObjects with unknown address returns not_found.
    [Fact]
    public async Task TestReadGroupObjectsNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_read_group_objects", Args.Of(("address", "9.9.9")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // device.readGroupObjects is a read -- it must NOT bump the revision.
    [Fact]
    public async Task TestReadGroupObjectsIsRead()
    {
        await using var host = await McpTestHost.StartAsync();
        var revBefore = host.Mock.Revision;
        await host.CallAsync("knx_read_group_objects", Args.Of(("address", "1.1.1")));
        Assert.Equal(revBefore, host.Mock.Revision);
    }

    // -- knx_read_group_objects dual-mode (async/job) -----------------------------

    // device.readGroupObjects with use_job=true returns {jobId}.
    [Fact]
    public async Task TestReadGroupObjectsAsyncReturnsJobId()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_read_group_objects", Args.Of(
            ("address", "1.1.1"),
            ("use_job", true))));
        Assert.True(data.HasB("jobId"));
        // Must NOT have comObjects at top level (it is a job reference)
        Assert.False(data.HasB("comObjects"));
    }

    // Polling the job from use_job=true yields the same shape as sync mode.
    [Fact]
    public async Task TestReadGroupObjectsAsyncJobResult()
    {
        await using var host = await McpTestHost.StartAsync();
        var jobId = ToolAssert.Object(await host.CallAsync("knx_read_group_objects", Args.Of(
            ("address", "1.1.1"),
            ("use_job", true))))["jobId"].StrB();

        var status = ToolAssert.Object(await host.CallAsync("knx_job_status", Args.Of(("job_id", jobId))));
        Assert.Equal("done", status["state"].StrB());
        var jobResult = status["result"].ObjB();
        Assert.Equal("1.1.1", jobResult["address"].StrB());
        jobResult["comObjects"].ArrB();
        jobResult["partial"].IsTrueB();
        JsonTestExtensionsB.AssertKindB(jobResult["note"], JsonValueKind.String);
    }

    // device.readGroupObjects without use_job returns result directly (default).
    [Fact]
    public async Task TestReadGroupObjectsSyncDefault()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_read_group_objects", Args.Of(("address", "1.1.1"))));
        // Sync mode: result has the payload directly, not a jobId
        Assert.True(data.HasB("comObjects"));
        Assert.False(data.HasB("jobId"));
    }

    // -- address normalisation for readGroupObjects --------------------------------

    // device.readGroupObjects tolerates comma-separated address.
    [Fact]
    public async Task TestReadGroupObjectsCommaAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_read_group_objects", Args.Of(("address", " 1, 1, 1 "))));
        Assert.Equal("1.1.1", data["address"].StrB());
        data["comObjects"].ArrB();
    }
}
