using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.Tests.Support;
using ModelContextProtocol.Client;
using ToolAnnotations = ModelContextProtocol.Protocol.ToolAnnotations;

namespace KnxEtsMcp.Tests;

/// <summary>Port of test_bus_safety.py: MCP tool annotations and the AddIn's "Bus read-only" mode.</summary>
public sealed class BusSafetyTests
{
    // Every tool that ends in a protocol method from ProtocolMethods.BusWrites.
    private static readonly HashSet<string> BusWriteTools = new(StringComparer.Ordinal)
    {
        "knx_program_device",
        "knx_update_firmware",
        "knx_group_write",
        "knx_device_unload",
        "knx_device_reset",
        "knx_bus_set_individual_address",
    };

    private static readonly HashSet<string> BusReadTools = new(StringComparer.Ordinal)
    {
        "knx_bus_ping",
        "knx_bus_scan_line",
        "knx_reconstruct_line",
        "knx_read_group_objects",
        "knx_device_read_info",
        "knx_group_read",
        "knx_group_monitor",
    };

    /// <summary>Tool name -> annotations (null when the tool has none).</summary>
    private static async Task<Dictionary<string, ToolAnnotations?>> ToolsAsync()
    {
        await using var host = await McpTestHost.StartAsync();
        var tools = await host.Client.ListToolsAsync();
        return tools.ToDictionary(t => t.Name, t => t.ProtocolTool.Annotations, StringComparer.Ordinal);
    }

    private static string[] Sorted(IEnumerable<string> names) => names.Order(StringComparer.Ordinal).ToArray();

    // -- Annotations ---------------------------------------------------------------

    [Fact]
    public async Task TestEveryToolIsAnnotated()
    {
        var tools = await ToolsAsync();
        var missing = tools
            .Where(t => t.Value is null || t.Value.ReadOnlyHint is null || t.Value.DestructiveHint is null)
            .Select(t => t.Key)
            .ToList();
        Assert.Empty(missing); // tools without safety annotations
    }

    [Fact]
    public async Task TestBusWriteToolsAreDestructiveAndOpenWorld()
    {
        var tools = await ToolsAsync();
        foreach (var name in BusWriteTools.Append("knx_job_cancel"))
        {
            var ann = tools[name];
            Assert.NotNull(ann);
            Assert.False(ann.ReadOnlyHint, name);
            Assert.True(ann.DestructiveHint, name);
            Assert.True(ann.OpenWorldHint, name);
        }
    }

    [Fact]
    public async Task TestBusReadToolsAreReadOnly()
    {
        var tools = await ToolsAsync();
        foreach (var name in BusReadTools)
        {
            var ann = tools[name];
            Assert.NotNull(ann);
            Assert.True(ann.ReadOnlyHint, name);
            Assert.True(ann.OpenWorldHint, name);
        }
    }

    [Fact]
    public async Task TestProjectReadsAreReadOnlyAndDeletesDestructive()
    {
        var tools = await ToolsAsync();
        foreach (var name in new[] { "knx_list_devices", "knx_list_group_addresses", "knx_list_parameters" })
        {
            var ann = tools[name];
            Assert.NotNull(ann);
            Assert.True(ann.ReadOnlyHint, name);
        }
        foreach (var name in new[] { "knx_delete_device", "knx_delete_group_address", "knx_unlink",
                                     "knx_set_parameter", "knx_batch_apply" })
        {
            var ann = tools[name];
            Assert.NotNull(ann);
            Assert.False(ann.ReadOnlyHint, name);
            Assert.True(ann.DestructiveHint, name);
        }
        var create = tools["knx_create_group_address"];
        Assert.NotNull(create);
        Assert.False(create.ReadOnlyHint);
        Assert.False(create.DestructiveHint);
    }

    [Fact]
    public async Task TestOnlyBusToolsAreOpenWorldBesidesOnlineCatalog()
    {
        var tools = await ToolsAsync();
        var openWorld = tools.Where(t => t.Value?.OpenWorldHint == true).Select(t => t.Key);
        var expected = BusWriteTools.Concat(BusReadTools).Concat(new[] { "knx_job_cancel", "knx_search_online_catalog" });
        Assert.Equal(Sorted(expected), Sorted(openWorld));
    }

    // -- Bus read-only mode (mock mirrors the AddIn's BusWritePolicy) --------------

    private static Task<McpTestHost> StartReadOnlyAsync()
        => McpTestHost.StartAsync(new MockTransport(busReadOnly: true));

    [Fact]
    public async Task TestReadOnlyBlocksProgramming()
    {
        await using var host = await StartReadOnlyAsync();
        var text = ToolAssert.ErrorText(await host.CallAsync("knx_program_device", Args.Of(("device_ref", "dev-0001"))));
        Assert.Contains("[approval_required]", text);
        Assert.Contains("Bus read-only", text);
    }

    [Fact]
    public async Task TestReadOnlyBlocksGroupWrite()
    {
        await using var host = await StartReadOnlyAsync();
        var result = await host.CallAsync("knx_group_write", Args.Of(("ga_ref", "ga-0001"), ("value", "01")));
        Assert.Contains("Bus read-only", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task TestReadOnlyStillAllowsBusReads()
    {
        await using var host = await StartReadOnlyAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_ping", Args.Of(("address", "1.1.1"))));
        data["alive"].IsTrueB();
    }

    [Fact]
    public async Task TestReadOnlyStillAllowsProjectEdits()
    {
        await using var host = await StartReadOnlyAsync();
        ToolAssert.Data(await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Kitchen light"),
            ("address", "1/1/99"))));
    }

    [Fact]
    public async Task TestProgrammingAllowedByDefault()
    {
        await using var host = await McpTestHost.StartAsync();
        ToolAssert.Data(await host.CallAsync("knx_program_device", Args.Of(("device_ref", "dev-0001"))));
    }

    [Fact]
    public void TestMockBusWriteSetMatchesTools()
    {
        var expected = new[]
        {
            "device.program", "firmware.update", "group.write",
            "device.unload", "device.reset", "bus.setIndividualAddress",
        };
        Assert.Equal(Sorted(expected), Sorted(ProtocolMethods.BusWrites));
    }
}
