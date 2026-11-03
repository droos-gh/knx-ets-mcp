using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Port of test_v0116.py: v0.1.16 AddIn contract -- label fields + 4 setter tools.</summary>
public sealed class V0116Tests
{
    // ---------------------------------------------------------------------------
    // The mutating-method set must include the 4 new setter methods
    // ---------------------------------------------------------------------------

    [Fact]
    public void TestMutatingMethodsIncludeLabelSetters()
    {
        Assert.Contains("device.setDescription", ProtocolMethods.Mutating);
        Assert.Contains("device.setComment", ProtocolMethods.Mutating);
        Assert.Contains("comObject.setDescription", ProtocolMethods.Mutating);
        Assert.Contains("comObject.setFunctionText", ProtocolMethods.Mutating);
    }

    // ---------------------------------------------------------------------------
    // Read-side: new label fields present in mock data
    // ---------------------------------------------------------------------------

    // devices.list returns description and comment fields.
    [Fact]
    public async Task TestDeviceHasDescriptionAndComment()
    {
        await using var host = await McpTestHost.StartAsync();
        var devices = ToolAssert.Data(await host.CallAsync("knx_list_devices"));
        var dev1 = devices.FindByRefB("dev-0001");
        Assert.True(dev1.HasB("description"));
        Assert.True(dev1.HasB("comment"));
        // Seeded value
        Assert.Equal("Couch", dev1["description"].StrB());
        Assert.Equal("Main actuator for living room", dev1["comment"].StrB());
    }

    // The seeded device with description='Couch' is discoverable.
    [Fact]
    public async Task TestDeviceDescriptionCouchDiscoverable()
    {
        await using var host = await McpTestHost.StartAsync();
        var devices = ToolAssert.Data(await host.CallAsync("knx_list_devices")).ObjectsB();
        var couch = Assert.Single(devices, d => d.GetStrB("description") == "Couch");
        Assert.Equal("dev-0001", couch["ref"].StrB());
    }

    // ga.list returns comment field (description already existed).
    [Fact]
    public async Task TestGroupAddressHasComment()
    {
        await using var host = await McpTestHost.StartAsync();
        var gas = ToolAssert.Data(await host.CallAsync("knx_list_group_addresses"));
        var ga1 = gas.FindByRefB("ga-0001");
        Assert.True(ga1.HasB("comment"));
        Assert.Equal("Ceiling light", ga1["comment"].StrB());
    }

    // comobjects.list returns description, functionText, text.
    [Fact]
    public async Task TestComobjectHasLabelFields()
    {
        await using var host = await McpTestHost.StartAsync();
        var cos = ToolAssert.Data(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        var co1 = cos.FindByRefB("co-0001");
        Assert.True(co1.HasB("description"));
        Assert.True(co1.HasB("functionText"));
        Assert.True(co1.HasB("text"));
        // Seeded value
        Assert.Equal("Raffstore links", co1["functionText"].StrB());
        Assert.Equal("Schalten Ausgang A", co1["text"].StrB());
    }

    // The seeded com object with functionText='Raffstore links' is discoverable.
    [Fact]
    public async Task TestComobjectFunctiontextRaffstoreDiscoverable()
    {
        await using var host = await McpTestHost.StartAsync();
        var cos = ToolAssert.Data(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001")))).ObjectsB();
        var raffstore = Assert.Single(cos, c => c.GetStrB("functionText") == "Raffstore links");
        Assert.Equal("co-0001", raffstore["ref"].StrB());
    }

    // topology.list returns description/comment on areas, lines, segments.
    [Fact]
    public async Task TestTopologyHasDescriptionComment()
    {
        await using var host = await McpTestHost.StartAsync();
        var areas = ToolAssert.Array(await host.CallAsync("knx_topology"));
        var area = areas[0];
        Assert.True(area.HasB("description"));
        Assert.True(area.HasB("comment"));
        var line = area!["lines"].ArrB()[0];
        Assert.True(line.HasB("description"));
        Assert.True(line.HasB("comment"));
        var seg = line!["segments"].ArrB()[0];
        Assert.True(seg.HasB("description"));
        Assert.True(seg.HasB("comment"));
    }

    // catalog.search results include description.
    [Fact]
    public async Task TestCatalogSearchHasDescription()
    {
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Actuator"))));
        Assert.NotEmpty(items);
        Assert.True(items[0].HasB("description"));
        Assert.NotEqual("", items[0].GetStrB("description"));
    }

    // device.channels returns text field in channel entries.
    [Fact]
    public async Task TestChannelsHaveText()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_channels", Args.Of(("device_ref", "dev-0001"))));
        var channels = data["channels"].ArrB();
        Assert.NotEmpty(channels);
        Assert.True(channels[0].HasB("text"));
    }

    // ---------------------------------------------------------------------------
    // Setter tools: set -> read roundtrip
    // ---------------------------------------------------------------------------

    // Set device description and verify via list.
    [Fact]
    public async Task TestSetDeviceDescriptionRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_device_description", Args.Of(
            ("device_ref", "dev-0001"),
            ("description", "Wohnzimmer Sideboard"))));
        Assert.Equal("Wohnzimmer Sideboard", data["description"].StrB());

        // Read back
        var devices = ToolAssert.Data(await host.CallAsync("knx_list_devices"));
        var dev = devices.FindByRefB("dev-0001");
        Assert.Equal("Wohnzimmer Sideboard", dev["description"].StrB());
    }

    // Set device comment and verify via list.
    [Fact]
    public async Task TestSetDeviceCommentRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_device_comment", Args.Of(
            ("device_ref", "dev-0002"),
            ("comment", "Needs rewiring"))));
        data["ok"].IsTrueB();

        // Read back
        var devices = ToolAssert.Data(await host.CallAsync("knx_list_devices"));
        var dev = devices.FindByRefB("dev-0002");
        Assert.Equal("Needs rewiring", dev["comment"].StrB());
    }

    // Set comobject description and verify via list.
    [Fact]
    public async Task TestSetComobjectDescriptionRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_comobject_description", Args.Of(
            ("com_object_ref", "co-0001"),
            ("description", "Jalousie Kanal A"))));
        Assert.Equal("co-0001", data["ref"].StrB());
        Assert.Equal("Jalousie Kanal A", data["description"].StrB());

        // Read back
        var cos = ToolAssert.Data(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        var co = cos.FindByRefB("co-0001");
        Assert.Equal("Jalousie Kanal A", co["description"].StrB());
    }

    // Set comobject functionText and verify via list.
    [Fact]
    public async Task TestSetComobjectFunctionTextRoundtrip()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_comobject_function_text", Args.Of(
            ("com_object_ref", "co-0001"),
            ("function_text", "Markise rechts"))));
        Assert.Equal("co-0001", data["ref"].StrB());
        Assert.Equal("Markise rechts", data["functionText"].StrB());

        // Read back
        var cos = ToolAssert.Data(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        var co = cos.FindByRefB("co-0001");
        Assert.Equal("Markise rechts", co["functionText"].StrB());
    }

    // ---------------------------------------------------------------------------
    // Setter tools: not_found errors
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TestSetDeviceDescriptionNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_device_description", Args.Of(
            ("device_ref", "dev-9999"),
            ("description", "x")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task TestSetDeviceCommentNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_device_comment", Args.Of(
            ("device_ref", "dev-9999"),
            ("comment", "x")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task TestSetComobjectDescriptionNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_comobject_description", Args.Of(
            ("com_object_ref", "co-9999"),
            ("description", "x")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task TestSetComobjectFunctionTextNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_comobject_function_text", Args.Of(
            ("com_object_ref", "co-9999"),
            ("function_text", "x")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // ---------------------------------------------------------------------------
    // Bridge-client-level roundtrip (no MCP layer)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TestBridgeClientSetDeviceDescription()
    {
        await using var bridge = new KnxBridgeClient(new MockTransport());
        var result = await bridge.RequestAsync("device.setDescription", new JsonObject
        {
            ["deviceRef"] = "dev-0001",
            ["description"] = "Esstisch",
        });
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal("Esstisch", result.Value?["description"].StrB());
    }

    [Fact]
    public async Task TestBridgeClientSetComobjectFunctionText()
    {
        await using var bridge = new KnxBridgeClient(new MockTransport());
        var result = await bridge.RequestAsync("comObject.setFunctionText", new JsonObject
        {
            ["comObjectRef"] = "co-0001",
            ["functionText"] = "Rollo Ost",
        });
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal("Rollo Ost", result.Value?["functionText"].StrB());
    }
}
