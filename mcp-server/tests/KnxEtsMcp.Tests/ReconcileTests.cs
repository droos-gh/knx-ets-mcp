using System.Text.Json;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Reconciliation methods device.unassign and device.compare (port of tests/test_reconcile.py).</summary>
public sealed class ReconcileTests
{
    // -- knx_device_unassign (device.unassign) --------------------------------

    [Fact]
    public async Task DeviceUnassign()
    {
        // Unassigning a device detaches it from its line (ok + revision bump).
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_unassign", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.BoolAt("ok"));

        // Verify the device still exists but has no line
        var devices = ToolAssert.Array(await host.CallAsync("knx_list_devices"));
        var dev = devices.TryFindBy("ref", "dev-0001");
        Assert.NotNull(dev);
        Assert.Null(dev.NodeAt("line"));
    }

    [Fact]
    public async Task DeviceUnassignNotFound()
    {
        // Unassigning a non-existent device returns not_found.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_device_unassign", Args.Of(("device_ref", "dev-NOPE")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task DeviceUnassignIdempotent()
    {
        // Unassigning an already-unassigned device succeeds (idempotent).
        await using var host = await McpTestHost.StartAsync();
        // First unassign
        await host.CallAsync("knx_device_unassign", Args.Of(("device_ref", "dev-0001")));
        // Second unassign should still succeed
        var data = ToolAssert.Object(await host.CallAsync("knx_device_unassign", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.BoolAt("ok"));
    }

    // -- knx_device_compare (device.compare) ----------------------------------

    [Fact]
    public async Task DeviceCompare()
    {
        // Comparing a device returns {compared, partial, note}.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_compare", Args.Of(("device_ref", "dev-0001"))));
        var compared = data.ArrayAt("compared");
        Assert.NotEmpty(compared);
        Assert.True(data.BoolAt("partial"));
        var note = data.NodeAt("note");
        Assert.NotNull(note);
        Assert.Equal(JsonValueKind.String, note.GetValueKind());

        // Check shape of each comparison entry
        Assert.All(compared.Objects(), entry =>
        {
            Assert.True(entry.ContainsKey("property"));
            Assert.True(entry.ContainsKey("equal"));
            Assert.True(entry.ContainsKey("expected"));
            Assert.True(entry.ContainsKey("actual"));
        });
    }

    [Fact]
    public async Task DeviceCompareNotFound()
    {
        // Comparing a non-existent device returns not_found.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_device_compare", Args.Of(("device_ref", "dev-NOPE")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task DeviceCompareIsRead()
    {
        // device.compare is a read -- it must NOT bump the revision.
        await using var host = await McpTestHost.StartAsync();
        var revBefore = host.Mock.Revision;
        await host.CallAsync("knx_device_compare", Args.Of(("device_ref", "dev-0001")));
        Assert.Equal(revBefore, host.Mock.Revision);
    }
}
