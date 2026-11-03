using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Port of test_g13.py: Phase G (v0.1.13) methods -- moves, additional addresses,
/// segments, trades, bus interface, param default, certificates, navigation, tags,
/// todos, channels, project history, device reset.
/// </summary>
public sealed class G13Tests
{
    private static async Task AssertErrorContainsAsync(string tool, IReadOnlyDictionary<string, object?> args, string code)
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync(tool, args);
        Assert.Contains(code, ToolAssert.ErrorText(result));
    }

    private static async Task AssertOkAsync(string tool, IReadOnlyDictionary<string, object?> args)
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync(tool, args));
        data["ok"].IsTrueB();
    }

    // -- device.reset (bus write) ------------------------------------------------

    [Fact]
    public Task TestDeviceReset()
        => AssertOkAsync("knx_device_reset", Args.Of(("device_ref", "dev-0001")));

    [Fact]
    public Task TestDeviceResetNotFound()
        => AssertErrorContainsAsync("knx_device_reset", Args.Of(("device_ref", "dev-9999")), "not_found");

    // -- groupRange.moveGroupAddress [M] -----------------------------------------

    [Fact]
    public Task TestMoveGroupAddress()
        => AssertOkAsync("knx_move_group_address", Args.Of(
            ("target_group_range_ref", "gr-main-1"),
            ("group_address_ref", "ga-0001")));

    [Fact]
    public Task TestMoveGroupAddressGaNotFound()
        => AssertErrorContainsAsync("knx_move_group_address", Args.Of(
            ("target_group_range_ref", "gr-main-1"),
            ("group_address_ref", "ga-9999")), "not_found");

    // -- groupRange.moveGroupRange [M] -------------------------------------------

    [Fact]
    public Task TestMoveGroupRange()
        => AssertOkAsync("knx_move_group_range", Args.Of(
            ("target_group_range_ref", "gr-main-1"),
            ("source_group_range_ref", "gr-mid-1-0")));

    [Fact]
    public Task TestMoveGroupRangeNotFound()
        => AssertErrorContainsAsync("knx_move_group_range", Args.Of(
            ("target_group_range_ref", "gr-main-1"),
            ("source_group_range_ref", "gr-9999")), "not_found");

    // -- line.move [M] -----------------------------------------------------------

    [Fact]
    public async Task TestMoveLine()
    {
        await using var host = await McpTestHost.StartAsync();
        // Create a second area first
        var areaB = ToolAssert.Object(await host.CallAsync("knx_create_area", Args.Of(("name", "Area B"))));

        var data = ToolAssert.Object(await host.CallAsync("knx_move_line", Args.Of(
            ("target_area_ref", areaB["areaRef"].StrB()),
            ("line_ref", "line-1.2"))));
        data["ok"].IsTrueB();
    }

    [Fact]
    public Task TestMoveLineNotFound()
        => AssertErrorContainsAsync("knx_move_line", Args.Of(
            ("target_area_ref", "area-1"),
            ("line_ref", "line-9.9")), "not_found");

    // -- buildingPart.move [M] ---------------------------------------------------

    [Fact]
    public Task TestMoveBuildingPart()
        => AssertOkAsync("knx_move_building_part", Args.Of(
            ("target_building_part_ref", "bp-bld-1"),
            ("source_building_part_ref", "bp-room-1")));

    [Fact]
    public Task TestMoveBuildingPartNotFound()
        => AssertErrorContainsAsync("knx_move_building_part", Args.Of(
            ("target_building_part_ref", "bp-bld-1"),
            ("source_building_part_ref", "bp-9999")), "not_found");

    // -- device.addAdditionalAddress [M] -----------------------------------------

    [Fact]
    public async Task TestAddDeviceAdditionalAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_add_device_additional_address", Args.Of(
            ("device_ref", "dev-0001"),
            ("address", 20))));
        Assert.Equal("dev-0001", data["deviceRef"].StrB());
        Assert.Equal(20, data["address"].IntB());
    }

    // Address 10 already exists in seed data
    [Fact]
    public Task TestAddDeviceAdditionalAddressDuplicate()
        => AssertErrorContainsAsync("knx_add_device_additional_address", Args.Of(
            ("device_ref", "dev-0001"),
            ("address", 10)), "invalid_params");

    // -- device.removeAdditionalAddress [M] --------------------------------------

    // Address 10 exists in seed data
    [Fact]
    public Task TestRemoveDeviceAdditionalAddress()
        => AssertOkAsync("knx_remove_device_additional_address", Args.Of(
            ("device_ref", "dev-0001"),
            ("address", 10)));

    [Fact]
    public Task TestRemoveDeviceAdditionalAddressNotFound()
        => AssertErrorContainsAsync("knx_remove_device_additional_address", Args.Of(
            ("device_ref", "dev-0001"),
            ("address", 99)), "not_found");

    // -- line.addAdditionalGroupAddress [M] --------------------------------------

    [Fact]
    public Task TestAddLineAdditionalGa()
        => AssertOkAsync("knx_add_line_additional_ga", Args.Of(
            ("line_ref", "line-1.1"),
            ("group_address_refs", new[] { "ga-0001" })));

    [Fact]
    public Task TestAddLineAdditionalGaLineNotFound()
        => AssertErrorContainsAsync("knx_add_line_additional_ga", Args.Of(
            ("line_ref", "line-9.9"),
            ("group_address_refs", new[] { "ga-0001" })), "not_found");

    // -- line.removeAdditionalGroupAddress [M] -----------------------------------

    [Fact]
    public async Task TestRemoveLineAdditionalGa()
    {
        await using var host = await McpTestHost.StartAsync();
        // First add, then remove
        await host.CallAsync("knx_add_line_additional_ga", Args.Of(
            ("line_ref", "line-1.1"),
            ("group_address_refs", new[] { "ga-0001" })));
        var data = ToolAssert.Object(await host.CallAsync("knx_remove_line_additional_ga", Args.Of(
            ("line_ref", "line-1.1"),
            ("group_address_refs", new[] { "ga-0001" }))));
        data["ok"].IsTrueB();
    }

    // -- segment.create [M] ------------------------------------------------------

    [Fact]
    public async Task TestCreateSegment()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_segment", Args.Of(
            ("line_ref", "line-1.1"),
            ("name", "New Segment"),
            ("medium_type", "TP"))));
        Assert.True(data.HasB("segmentRef"));
        Assert.Equal("New Segment", data["name"].StrB());
    }

    [Fact]
    public Task TestCreateSegmentLineNotFound()
        => AssertErrorContainsAsync("knx_create_segment", Args.Of(
            ("line_ref", "line-9.9"),
            ("name", "Seg"),
            ("medium_type", "TP")), "not_found");

    // -- segment.delete [M] ------------------------------------------------------

    // Seed has seg-1.1.0
    [Fact]
    public Task TestDeleteSegment()
        => AssertOkAsync("knx_delete_segment", Args.Of(("segment_ref", "seg-1.1.0")));

    [Fact]
    public Task TestDeleteSegmentNotFound()
        => AssertErrorContainsAsync("knx_delete_segment", Args.Of(("segment_ref", "seg-9.9.9")), "not_found");

    // -- trades.list [R] ---------------------------------------------------------

    [Fact]
    public async Task TestListTrades()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_list_trades"));
        Assert.NotEmpty(data);
        var t = data[0];
        Assert.True(t.HasB("ref"));
        Assert.True(t.HasB("name"));
        Assert.True(t.HasB("number"));
        Assert.True(t.HasB("devices"));
    }

    // -- trade.create [M] --------------------------------------------------------

    [Fact]
    public async Task TestCreateTrade()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_trade", Args.Of(("name", "Electrical"))));
        Assert.Equal("Electrical", data["name"].StrB());
        Assert.True(data.HasB("ref"));
        Assert.True(data.HasB("number"));
    }

    // -- trade.delete [M] --------------------------------------------------------

    [Fact]
    public Task TestDeleteTrade()
        => AssertOkAsync("knx_delete_trade", Args.Of(("trade_ref", "trade-0001")));

    [Fact]
    public Task TestDeleteTradeNotFound()
        => AssertErrorContainsAsync("knx_delete_trade", Args.Of(("trade_ref", "trade-9999")), "not_found");

    // -- trade.assignDevice [M] --------------------------------------------------

    [Fact]
    public Task TestTradeAssignDevice()
        => AssertOkAsync("knx_trade_assign_device", Args.Of(
            ("trade_ref", "trade-0001"),
            ("device_ref", "dev-0002")));

    [Fact]
    public Task TestTradeAssignDeviceTradeNotFound()
        => AssertErrorContainsAsync("knx_trade_assign_device", Args.Of(
            ("trade_ref", "trade-9999"),
            ("device_ref", "dev-0001")), "not_found");

    // -- trade.unassignDevice [M] ------------------------------------------------

    // dev-0001 is assigned in seed data
    [Fact]
    public Task TestTradeUnassignDevice()
        => AssertOkAsync("knx_trade_unassign_device", Args.Of(
            ("trade_ref", "trade-0001"),
            ("device_ref", "dev-0001")));

    // -- busInterface.link [M] ---------------------------------------------------

    [Fact]
    public Task TestBusInterfaceLink()
        => AssertOkAsync("knx_bus_interface_link", Args.Of(
            ("device_ref", "dev-0001"),
            ("group_address_refs", new[] { "ga-0001", "ga-0002" })));

    [Fact]
    public Task TestBusInterfaceLinkDeviceNotFound()
        => AssertErrorContainsAsync("knx_bus_interface_link", Args.Of(
            ("device_ref", "dev-9999"),
            ("group_address_refs", new[] { "ga-0001" })), "not_found");

    // -- busInterface.unlink [M] -------------------------------------------------

    [Fact]
    public async Task TestBusInterfaceUnlink()
    {
        await using var host = await McpTestHost.StartAsync();
        // First link, then unlink
        await host.CallAsync("knx_bus_interface_link", Args.Of(
            ("device_ref", "dev-0001"),
            ("group_address_refs", new[] { "ga-0001" })));
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_interface_unlink", Args.Of(
            ("device_ref", "dev-0001"),
            ("group_address_refs", new[] { "ga-0001" }))));
        data["ok"].IsTrueB();
    }

    // -- param.setDefault [M] ----------------------------------------------------

    // param-0002 has value "3", default "5"
    [Fact]
    public Task TestSetParameterDefault()
        => AssertOkAsync("knx_set_parameter_default", Args.Of(
            ("device_ref", "dev-0002"),
            ("parameter_ref", "param-0002")));

    // param-0002 belongs to dev-0002
    [Fact]
    public Task TestSetParameterDefaultWrongDevice()
        => AssertErrorContainsAsync("knx_set_parameter_default", Args.Of(
            ("device_ref", "dev-0001"),
            ("parameter_ref", "param-0002")), "invalid_params");

    // -- certificates.list [R] ---------------------------------------------------

    [Fact]
    public async Task TestListCertificates()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_list_certificates"));
        Assert.NotEmpty(data);
        var c = data[0];
        Assert.True(c.HasB("ref"));
        Assert.True(c.HasB("serialNumber"));
        Assert.True(c.HasB("deviceRef"));
        // hasDevice must NOT be present (no secrets in cert DTO)
        Assert.False(c.HasB("hasDevice"));
    }

    // -- certificate.add [M] -----------------------------------------------------

    [Fact]
    public async Task TestAddCertificate()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_add_certificate", Args.Of(
            ("value", "ABCDE12345FGHIJ67890KLMNO"))));
        Assert.True(data.HasB("ref"));
    }

    // -- certificate.delete [M] --------------------------------------------------

    [Fact]
    public Task TestDeleteCertificate()
        => AssertOkAsync("knx_delete_certificate", Args.Of(("certificate_ref", "cert-0001")));

    [Fact]
    public Task TestDeleteCertificateNotFound()
        => AssertErrorContainsAsync("knx_delete_certificate", Args.Of(("certificate_ref", "cert-9999")), "not_found");

    // -- project.navigateTo [R/command] ------------------------------------------

    [Fact]
    public Task TestNavigateTo()
        => AssertOkAsync("knx_navigate_to", Args.Of(("ref", "dev-0001")));

    // -- tags.list [R] -----------------------------------------------------------

    [Fact]
    public async Task TestListTags()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_list_tags"));
        Assert.NotEmpty(data);
        var t = data[0];
        Assert.True(t.HasB("ref"));
        Assert.True(t.HasB("label"));
        Assert.True(t.HasB("color"));
    }

    // -- tag.create [M] ----------------------------------------------------------

    [Fact]
    public async Task TestCreateTag()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_tag", Args.Of(
            ("label", "Reviewed"),
            ("color", "#00FF00"))));
        Assert.Equal("Reviewed", data["label"].StrB());
        Assert.Equal("#00FF00", data["color"].StrB());
        Assert.True(data.HasB("ref"));
    }

    [Fact]
    public async Task TestCreateTagDefaultColor()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_tag", Args.Of(("label", "Draft"))));
        Assert.Equal("Draft", data["label"].StrB());
        Assert.True(data.HasB("color"));
    }

    // -- tag.delete [M] ----------------------------------------------------------

    [Fact]
    public Task TestDeleteTag()
        => AssertOkAsync("knx_delete_tag", Args.Of(("tag_ref", "tag:t1")));

    [Fact]
    public Task TestDeleteTagNotFound()
        => AssertErrorContainsAsync("knx_delete_tag", Args.Of(("tag_ref", "tag-9999")), "not_found");

    // -- todos.list [R] ----------------------------------------------------------

    [Fact]
    public async Task TestListTodos()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_list_todos"));
        Assert.NotEmpty(data);
        var t = data[0];
        Assert.True(t.HasB("ref"));
        Assert.True(t.HasB("description"));
        Assert.True(t.HasB("status"));
    }

    // -- todo.create [M] ---------------------------------------------------------

    [Fact]
    public async Task TestCreateTodo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_todo", Args.Of(
            ("description", "Verify addresses"),
            ("object_path", "Area 1/Line 1"),
            ("status", "Open"))));
        Assert.Equal("Verify addresses", data["description"].StrB());
        Assert.True(data.HasB("ref"));
        Assert.Equal("Open", data["status"].StrB());
    }

    [Fact]
    public async Task TestCreateTodoMinimal()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_todo", Args.Of(("description", "Quick check"))));
        Assert.Equal("Quick check", data["description"].StrB());
        Assert.Equal("Open", data["status"].StrB());
    }

    // -- todo.delete [M] ---------------------------------------------------------

    [Fact]
    public Task TestDeleteTodo()
        => AssertOkAsync("knx_delete_todo", Args.Of(("todo_ref", "todo:t1")));

    [Fact]
    public Task TestDeleteTodoNotFound()
        => AssertErrorContainsAsync("knx_delete_todo", Args.Of(("todo_ref", "todo-9999")), "not_found");

    // -- device.channels [R] -----------------------------------------------------

    [Fact]
    public async Task TestDeviceChannels()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_channels", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.HasB("channels"));
        Assert.True(data.HasB("modules"));
        Assert.NotEmpty(data["channels"].ArrB());
    }

    [Fact]
    public Task TestDeviceChannelsNotFound()
        => AssertErrorContainsAsync("knx_device_channels", Args.Of(("device_ref", "dev-9999")), "not_found");

    // -- projectHistory.list [R] -------------------------------------------------

    [Fact]
    public async Task TestListProjectHistory()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_list_project_history"));
        Assert.NotEmpty(data);
        var h = data[0];
        Assert.True(h.HasB("ref"));
        Assert.True(h.HasB("date"));
        Assert.True(h.HasB("text"));
        Assert.True(h.HasB("user"));
    }

    // -- projectHistory.add [M] --------------------------------------------------

    [Fact]
    public async Task TestAddProjectHistory()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_add_project_history", Args.Of(
            ("text", "Added dimmer via MCP"))));
        Assert.Equal("Added dimmer via MCP", data["text"].StrB());
        Assert.True(data.HasB("ref"));
        Assert.True(data.HasB("date"));
    }

    // -- projectHistory.delete [M] -----------------------------------------------

    [Fact]
    public Task TestDeleteProjectHistory()
        => AssertOkAsync("knx_delete_project_history", Args.Of(("history_ref", "ph:h1")));

    [Fact]
    public Task TestDeleteProjectHistoryNotFound()
        => AssertErrorContainsAsync("knx_delete_project_history", Args.Of(("history_ref", "hist-9999")), "not_found");

    // ===========================================================================
    // Mock-parity assertions (Codex findings)
    // ===========================================================================

    // Every mock device must include orderNumber (C# DeviceInfo).
    [Fact]
    public async Task TestDeviceHasOrderNumber()
    {
        await using var host = await McpTestHost.StartAsync();
        var devices = ToolAssert.Data(await host.CallAsync("knx_list_devices")).ObjectsB();
        foreach (var d in devices)
        {
            Assert.True(d.HasB("orderNumber"), $"Device {d.GetStrB("ref")} missing orderNumber");
            d["orderNumber"].StrB();
        }
    }

    // Tag color must be #RRGGBB (production returns leading '#').
    [Fact]
    public async Task TestTagColorIsHashPrefixed()
    {
        await using var host = await McpTestHost.StartAsync();
        var tags = ToolAssert.Data(await host.CallAsync("knx_list_tags")).ObjectsB();
        foreach (var t in tags)
        {
            var color = t["color"].StrB();
            Assert.StartsWith("#", color, StringComparison.Ordinal);
            Assert.Equal(7, color.Length);
        }
    }

    // Todo status must be Open or Accomplished (not lowercase).
    [Fact]
    public async Task TestTodoStatusCanonicalCasing()
    {
        await using var host = await McpTestHost.StartAsync();
        var todos = ToolAssert.Data(await host.CallAsync("knx_list_todos")).ObjectsB();
        var valid = new[] { "Open", "Accomplished" };
        foreach (var t in todos)
            Assert.Contains(t["status"].StrB(), valid);
    }

    // Creating a todo with invalid status returns error (lowercase is now invalid).
    [Fact]
    public Task TestCreateTodoInvalidStatus()
        => AssertErrorContainsAsync("knx_create_todo", Args.Of(
            ("description", "Bad status"),
            ("status", "open")), "invalid_params");

    // -- ID-based ref roundtrips ---------------------------------------------------

    // Create a tag, then delete it using the returned ref.
    [Fact]
    public async Task TestTagCreateDeleteRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var tag = ToolAssert.Object(await host.CallAsync("knx_create_tag", Args.Of(
            ("label", "Roundtrip"),
            ("color", "#AABB00"))));
        var reference = tag["ref"].StrB();
        Assert.StartsWith("tag:", reference, StringComparison.Ordinal);

        var data = ToolAssert.Object(await host.CallAsync("knx_delete_tag", Args.Of(("tag_ref", reference))));
        data["ok"].IsTrueB();
    }

    // Create a todo, then delete it using the returned ref.
    [Fact]
    public async Task TestTodoCreateDeleteRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var todo = ToolAssert.Object(await host.CallAsync("knx_create_todo", Args.Of(("description", "Roundtrip todo"))));
        var reference = todo["ref"].StrB();
        Assert.StartsWith("todo:", reference, StringComparison.Ordinal);

        var data = ToolAssert.Object(await host.CallAsync("knx_delete_todo", Args.Of(("todo_ref", reference))));
        data["ok"].IsTrueB();
    }

    // Create a history entry, then delete it using the returned ref.
    [Fact]
    public async Task TestProjectHistoryCreateDeleteRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var entry = ToolAssert.Object(await host.CallAsync("knx_add_project_history", Args.Of(("text", "Roundtrip entry"))));
        var reference = entry["ref"].StrB();
        Assert.StartsWith("ph:", reference, StringComparison.Ordinal);

        var data = ToolAssert.Object(await host.CallAsync("knx_delete_project_history", Args.Of(("history_ref", reference))));
        data["ok"].IsTrueB();
    }

    // -- Address normalisation (bus.ping) ------------------------------------------

    // bus.ping tolerates comma-separated address with whitespace.
    [Fact]
    public async Task TestBusPingCommaAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_ping", Args.Of(("address", " 1, 1, 5 "))));
        data["alive"].IsTrueB();
    }

    // bus.ping with comma address ending in 0 -> not alive.
    [Fact]
    public async Task TestBusPingCommaAddressNotAlive()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_ping", Args.Of(("address", "1,1,0"))));
        data["alive"].IsFalseB();
    }

    // -- Channels match C# DeviceChannelsResult ------------------------------------

    // device.channels result includes deviceRef (C# DeviceChannelsResult).
    [Fact]
    public async Task TestDeviceChannelsHasDeviceRef()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_channels", Args.Of(("device_ref", "dev-0001"))));
        Assert.Equal("dev-0001", data["deviceRef"].StrB());
    }

    // Channel entries match C# ChannelInstanceInfo fields.
    [Fact]
    public async Task TestDeviceChannelsShapeMirrorsCsharp()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_channels", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.HasB("channels"));
        Assert.True(data.HasB("modules"));
        data["modules"].ArrB();
        foreach (var ch in data["channels"].ObjectsB())
        {
            Assert.True(ch.HasB("name"));
            Assert.True(ch.HasB("isActive"));
            Assert.True(ch.HasB("activeComObjectRefs"));
            ch["activeComObjectRefs"].ArrB();
            // description and applicationProgramChannelId are nullable
            Assert.True(ch.HasB("description"));
            Assert.True(ch.HasB("applicationProgramChannelId"));
        }
    }

    // -- Certificate add returns full DTO (no secrets) ----------------------------

    // certificate.add returns {ref, serialNumber, deviceRef}.
    [Fact]
    public async Task TestCertificateAddReturnsDto()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_add_certificate", Args.Of(
            ("value", "ABCDE12345FGHIJ67890KLMNO"))));
        Assert.True(data.HasB("ref"));
        Assert.True(data.HasB("serialNumber"));
        Assert.True(data.HasB("deviceRef"));
        Assert.Null(data["deviceRef"]); // newly added cert has no device
        Assert.False(data.HasB("hasDevice"));
        Assert.False(data.HasB("fdsk"));
        Assert.False(data.HasB("password"));
    }
}
