using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Phase A mutation tools (port of tests/test_phase_a.py).</summary>
public sealed class PhaseATests
{
    // -- knx_delete_group_address ---------------------------------------------

    [Fact]
    public async Task DeleteGroupAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_delete_group_address", Args.Of(("ga_ref", "ga-0001"))));
        Assert.True(data.BoolAt("ok"));

        // Verify it's gone
        var gas = ToolAssert.Array(await host.CallAsync("knx_list_group_addresses"));
        Assert.All(gas.Objects(), ga => Assert.NotEqual("ga-0001", ga.StringAt("ref")));
    }

    [Fact]
    public async Task DeleteGroupAddressNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_group_address", Args.Of(("ga_ref", "ga-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_rename_group_address ---------------------------------------------

    [Fact]
    public async Task RenameGroupAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_rename_group_address", Args.Of(
            ("ga_ref", "ga-0001"),
            ("name", "Renamed GA"))));
        Assert.Equal("Renamed GA", data.StringAt("name"));
        Assert.Equal("ga-0001", data.StringAt("ref"));
    }

    [Fact]
    public async Task RenameGroupAddressNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_rename_group_address", Args.Of(
            ("ga_ref", "ga-9999"),
            ("name", "X")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_set_group_address_description ------------------------------------

    [Fact]
    public async Task SetGroupAddressDescription()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_group_address_description", Args.Of(
            ("ga_ref", "ga-0001"),
            ("description", "Controls the living room light"))));
        Assert.Equal("Controls the living room light", data.StringAt("description"));
    }

    // -- knx_set_group_address_dpt --------------------------------------------

    [Fact]
    public async Task SetGroupAddressDpt()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_group_address_dpt", Args.Of(
            ("ga_ref", "ga-0001"),
            ("dpt_main", 5),
            ("dpt_sub", 1))));
        Assert.Equal("5.001", data.StringAt("dpt"));
    }

    [Fact]
    public async Task SetGroupAddressDptNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_group_address_dpt", Args.Of(
            ("ga_ref", "ga-9999"),
            ("dpt_main", 1)));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_create_group_range -----------------------------------------------

    [Fact]
    public async Task CreateGroupRange()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_group_range", Args.Of(
            ("name", "HVAC"),
            ("address", 3))));
        Assert.True(data.ContainsKey("ref"));
        Assert.Equal("HVAC", data.StringAt("name"));
        Assert.Equal(3, data.IntAt("address"));
    }

    [Fact]
    public async Task CreateGroupRangeNested()
    {
        await using var host = await McpTestHost.StartAsync();
        // Create parent first
        var parentData = ToolAssert.Object(await host.CallAsync("knx_create_group_range", Args.Of(
            ("name", "Parent Group"),
            ("address", 5))));

        var data = ToolAssert.Object(await host.CallAsync("knx_create_group_range", Args.Of(
            ("name", "Child Group"),
            ("address", 2),
            ("parent_group_range_ref", parentData.StringAt("ref")))));
        Assert.Equal("Child Group", data.StringAt("name"));
        Assert.Equal(2, data.IntAt("address"));
    }

    // -- knx_delete_group_range -----------------------------------------------

    [Fact]
    public async Task DeleteGroupRange()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_delete_group_range", Args.Of(("group_range_ref", "gr-main-1"))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task DeleteGroupRangeNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_group_range", Args.Of(("group_range_ref", "gr-nonexistent")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_delete_device ----------------------------------------------------

    [Fact]
    public async Task DeleteDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_delete_device", Args.Of(("device_ref", "dev-0002"))));
        Assert.True(data.BoolAt("ok"));

        // Verify it's gone
        var devs = ToolAssert.Array(await host.CallAsync("knx_list_devices"));
        Assert.All(devs.Objects(), d => Assert.NotEqual("dev-0002", d.StringAt("ref")));
    }

    [Fact]
    public async Task DeleteDeviceNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_device", Args.Of(("device_ref", "dev-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_rename_device ----------------------------------------------------

    [Fact]
    public async Task RenameDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_rename_device", Args.Of(
            ("device_ref", "dev-0001"),
            ("name", "Renamed Device"))));
        Assert.Equal("Renamed Device", data.StringAt("name"));
        Assert.Equal("dev-0001", data.StringAt("ref"));
    }

    // -- knx_set_device_address -----------------------------------------------

    [Fact]
    public async Task SetDeviceAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_device_address", Args.Of(
            ("device_ref", "dev-0001"),
            ("address", "1.1.5"))));
        Assert.Equal("1.1.5", data.StringAt("address"));
    }

    // -- knx_move_device ------------------------------------------------------

    [Fact]
    public async Task MoveDeviceNotSupported()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_move_device", Args.Of(
            ("device_ref", "dev-0001"),
            ("line_ref", "line-1.2")));
        Assert.Contains("not_supported", ToolAssert.ErrorText(result));
    }

    // -- knx_create_area ------------------------------------------------------

    [Fact]
    public async Task CreateArea()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_area", Args.Of(
            ("name", "Building B"),
            ("address", 2))));
        Assert.True(data.ContainsKey("areaRef"));
        Assert.Equal("Building B", data.StringAt("name"));

        // Verify in topology
        var areas = ToolAssert.Array(await host.CallAsync("knx_topology"));
        Assert.Contains(areas.Objects(), a => a.StringAt("name") == "Building B");
    }

    // -- knx_delete_area ------------------------------------------------------

    [Fact]
    public async Task DeleteArea()
    {
        await using var host = await McpTestHost.StartAsync();
        // Create then delete
        var areaRef = ToolAssert.Object(await host.CallAsync("knx_create_area", Args.Of(
            ("name", "Temp Area"),
            ("address", 3)))).StringAt("areaRef");

        var data = ToolAssert.Object(await host.CallAsync("knx_delete_area", Args.Of(("area_ref", areaRef))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task DeleteAreaNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_area", Args.Of(("area_ref", "area-999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_create_line ------------------------------------------------------

    [Fact]
    public async Task CreateLine()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_line", Args.Of(
            ("area_ref", "area-1"),
            ("name", "Third Line"),
            ("address", 3))));
        Assert.True(data.ContainsKey("lineRef"));
        Assert.Equal("Third Line", data.StringAt("name"));
    }

    [Fact]
    public async Task CreateLineAreaNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_create_line", Args.Of(
            ("area_ref", "area-999"),
            ("name", "Orphan")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_delete_line ------------------------------------------------------

    [Fact]
    public async Task DeleteLine()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_delete_line", Args.Of(("line_ref", "line-1.2"))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task DeleteLineNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_line", Args.Of(("line_ref", "line-99.99")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_set_comobject_flags ----------------------------------------------

    [Fact]
    public async Task SetComobjectFlags()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_comobject_flags", Args.Of(
            ("com_object_ref", "co-0001"),
            ("write_flag", false),
            ("transmit_flag", true),
            ("priority", "High"))));
        Assert.Equal("co-0001", data.StringAt("ref"));
        Assert.False(data.BoolAt("writeFlag"));
        Assert.True(data.BoolAt("transmitFlag"));
        Assert.Equal("High", data.StringAt("priority"));
        // Unchanged flags should retain their original values
        Assert.True(data.BoolAt("readFlag"));
    }

    [Fact]
    public async Task SetComobjectFlagsInactive()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_comobject_flags", Args.Of(
            ("com_object_ref", "co-0003"),
            ("read_flag", true)));
        Assert.Contains("inactive_object", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task SetComobjectFlagsNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_comobject_flags", Args.Of(
            ("com_object_ref", "co-9999"),
            ("read_flag", true)));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task SetComobjectFlagsInvalidPriority()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_comobject_flags", Args.Of(
            ("com_object_ref", "co-0001"),
            ("priority", "Invalid")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }
}
