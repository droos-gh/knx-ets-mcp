using System.Text.Json.Nodes;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Phase B: building structure + building function tools (port of tests/test_phase_b.py).</summary>
public sealed class PhaseBTests
{
    /// <summary>The seeded room: building[0].children[0].children[0].</summary>
    private static JsonObject SeedRoom(JsonArray building)
        => building[0].ArrayAt("children")[0].ArrayAt("children")[0].AsObj();

    // -- knx_list_building ----------------------------------------------------

    [Fact]
    public async Task ListBuilding()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_list_building"));
        var bld = Assert.Single(data).AsObj();  // One root: Main Building

        Assert.Equal("Main Building", bld.StringAt("name"));
        Assert.Equal("Building", bld.StringAt("type"));
        var floor = Assert.Single(bld.ArrayAt("children")).AsObj();

        Assert.Equal("Ground Floor", floor.StringAt("name"));
        Assert.Equal("Floor", floor.StringAt("type"));
        var room = Assert.Single(floor.ArrayAt("children")).AsObj();

        Assert.Equal("Living Room", room.StringAt("name"));
        Assert.Equal("Room", room.StringAt("type"));
        Assert.Contains("dev-0001", room.ArrayAt("devices").Strings());

        // Check building function in room
        var bf = Assert.Single(room.ArrayAt("functions")).AsObj();
        Assert.Equal("Lighting Control", bf.StringAt("name"));
        Assert.Contains("ga-0001", bf.ArrayAt("groupAddresses").Strings());
    }

    // -- knx_create_building_part ---------------------------------------------

    [Fact]
    public async Task CreateBuildingPart()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_building_part", Args.Of(
            ("name", "Basement"),
            ("type", "Floor"),
            ("parent_building_part_ref", "bp-bld-1"))));
        Assert.Equal("Basement", data.StringAt("name"));
        Assert.Equal("Floor", data.StringAt("type"));
        Assert.True(data.ContainsKey("ref"));

        // Verify it appears in building list
        var bld = ToolAssert.Array(await host.CallAsync("knx_list_building"))[0];
        Assert.Contains("Basement", bld.ArrayAt("children").StringsAt("name"));
    }

    [Fact]
    public async Task CreateBuildingPartTopLevel()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_building_part", Args.Of(
            ("name", "Annex"),
            ("type", "Building"))));
        Assert.Equal("Annex", data.StringAt("name"));
        Assert.Equal("Building", data.StringAt("type"));
    }

    [Fact]
    public async Task CreateBuildingPartInvalidType()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_create_building_part", Args.Of(
            ("name", "Bad"),
            ("type", "Garage")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task CreateBuildingPartParentNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_create_building_part", Args.Of(
            ("name", "Orphan"),
            ("type", "Room"),
            ("parent_building_part_ref", "bp-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_delete_building_part ---------------------------------------------

    [Fact]
    public async Task DeleteBuildingPart()
    {
        await using var host = await McpTestHost.StartAsync();
        // Create then delete
        var bpRef = ToolAssert.Object(await host.CallAsync("knx_create_building_part", Args.Of(
            ("name", "Temp Room"),
            ("type", "Room"),
            ("parent_building_part_ref", "bp-floor-1")))).StringAt("ref");

        var data = ToolAssert.Object(await host.CallAsync("knx_delete_building_part", Args.Of(("building_part_ref", bpRef))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task DeleteBuildingPartNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_building_part", Args.Of(("building_part_ref", "bp-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_rename_building_part ---------------------------------------------

    [Fact]
    public async Task RenameBuildingPart()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_rename_building_part", Args.Of(
            ("building_part_ref", "bp-room-1"),
            ("name", "Master Bedroom"))));
        Assert.Equal("Master Bedroom", data.StringAt("name"));
        Assert.Equal("bp-room-1", data.StringAt("ref"));
    }

    [Fact]
    public async Task RenameBuildingPartNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_rename_building_part", Args.Of(
            ("building_part_ref", "bp-9999"),
            ("name", "X")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_assign_device / knx_unassign_device ------------------------------

    [Fact]
    public async Task AssignDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_assign_device", Args.Of(
            ("building_part_ref", "bp-room-1"),
            ("device_ref", "dev-0002"))));
        Assert.True(data.BoolAt("ok"));

        // Verify device appears in listing
        var room = SeedRoom(ToolAssert.Array(await host.CallAsync("knx_list_building")));
        Assert.Contains("dev-0002", room.ArrayAt("devices").Strings());
    }

    [Fact]
    public async Task AssignDeviceNotFoundBp()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_assign_device", Args.Of(
            ("building_part_ref", "bp-9999"),
            ("device_ref", "dev-0001")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task AssignDeviceNotFoundDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_assign_device", Args.Of(
            ("building_part_ref", "bp-room-1"),
            ("device_ref", "dev-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task UnassignDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_unassign_device", Args.Of(
            ("building_part_ref", "bp-room-1"),
            ("device_ref", "dev-0001"))));
        Assert.True(data.BoolAt("ok"));

        // Verify device no longer in listing
        var room = SeedRoom(ToolAssert.Array(await host.CallAsync("knx_list_building")));
        Assert.DoesNotContain("dev-0001", room.ArrayAt("devices").Strings());
    }

    [Fact]
    public async Task UnassignDeviceNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_unassign_device", Args.Of(
            ("building_part_ref", "bp-9999"),
            ("device_ref", "dev-0001")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_create_building_function -----------------------------------------

    [Fact]
    public async Task CreateBuildingFunction()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_building_function", Args.Of(
            ("building_part_ref", "bp-room-1"),
            ("name", "HVAC Control"))));
        Assert.Equal("HVAC Control", data.StringAt("name"));
        Assert.True(data.ContainsKey("ref"));
        Assert.Empty(data.ArrayAt("groupAddresses"));
    }

    [Fact]
    public async Task CreateBuildingFunctionBpNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_create_building_function", Args.Of(
            ("building_part_ref", "bp-9999"),
            ("name", "X")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_delete_building_function -----------------------------------------

    [Fact]
    public async Task DeleteBuildingFunction()
    {
        await using var host = await McpTestHost.StartAsync();
        // Create then delete
        var bfRef = ToolAssert.Object(await host.CallAsync("knx_create_building_function", Args.Of(
            ("building_part_ref", "bp-room-1"),
            ("name", "Temp Function")))).StringAt("ref");

        var data = ToolAssert.Object(await host.CallAsync("knx_delete_building_function", Args.Of(("building_function_ref", bfRef))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task DeleteBuildingFunctionNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_delete_building_function", Args.Of(("building_function_ref", "bf-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_link_building_function_ga / knx_unlink_building_function_ga ------

    [Fact]
    public async Task LinkBuildingFunctionGa()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_link_building_function_ga", Args.Of(
            ("building_function_ref", "bf-0001"),
            ("group_address_refs", new[] { "ga-0002" }))));
        Assert.True(data.BoolAt("ok"));

        // Verify in building list
        var room = SeedRoom(ToolAssert.Array(await host.CallAsync("knx_list_building")));
        var bf = room.ArrayAt("functions").FindBy("ref", "bf-0001");
        Assert.Contains("ga-0002", bf.ArrayAt("groupAddresses").Strings());
    }

    [Fact]
    public async Task LinkBuildingFunctionGaNotFoundBf()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_link_building_function_ga", Args.Of(
            ("building_function_ref", "bf-9999"),
            ("group_address_refs", new[] { "ga-0001" })));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task LinkBuildingFunctionGaNotFoundGa()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_link_building_function_ga", Args.Of(
            ("building_function_ref", "bf-0001"),
            ("group_address_refs", new[] { "ga-9999" })));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task UnlinkBuildingFunctionGa()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_unlink_building_function_ga", Args.Of(
            ("building_function_ref", "bf-0001"),
            ("group_address_refs", new[] { "ga-0001" }))));
        Assert.True(data.BoolAt("ok"));

        // Verify GA removed
        var room = SeedRoom(ToolAssert.Array(await host.CallAsync("knx_list_building")));
        var bf = room.ArrayAt("functions").FindBy("ref", "bf-0001");
        Assert.DoesNotContain("ga-0001", bf.ArrayAt("groupAddresses").Strings());
    }

    [Fact]
    public async Task UnlinkBuildingFunctionGaNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_unlink_building_function_ga", Args.Of(
            ("building_function_ref", "bf-9999"),
            ("group_address_refs", new[] { "ga-0001" })));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }
}
