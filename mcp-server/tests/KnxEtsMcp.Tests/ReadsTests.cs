using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Read-only tools over the MCP layer (port of tests/test_reads.py).</summary>
public sealed class ReadsTests
{
    // -- knx_project_info -----------------------------------------------------

    [Fact]
    public async Task ProjectInfo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_project_info"));
        Assert.Equal("proj-001", data.StringAt("projectId"));
        Assert.Equal("Test KNX Project", data.StringAt("name"));
        Assert.Equal("ThreeLevel", data.StringAt("groupAddressStyle"));
        Assert.True(data.ContainsKey("revision"));
    }

    // -- knx_list_devices -----------------------------------------------------

    [Fact]
    public async Task ListDevices()
    {
        await using var host = await McpTestHost.StartAsync();
        var devices = ToolAssert.Array(await host.CallAsync("knx_list_devices"));
        Assert.Equal(2, devices.Count);
        var refs = devices.StringsAt("ref");
        Assert.Contains("dev-0001", refs);
        Assert.Contains("dev-0002", refs);

        var sw = devices.FindBy("ref", "dev-0001");
        Assert.Equal("Switch Actuator 4x", sw.StringAt("name"));
        Assert.Equal("1.1.1", sw.StringAt("address"));
    }

    // -- knx_list_group_addresses ---------------------------------------------

    [Fact]
    public async Task ListGroupAddresses()
    {
        await using var host = await McpTestHost.StartAsync();
        var gas = ToolAssert.Array(await host.CallAsync("knx_list_group_addresses"));
        Assert.Equal(2, gas.Count);
        var names = gas.StringsAt("name");
        Assert.Contains("Light Living Room", names);
        Assert.Contains("Dimmer Living Room", names);
    }

    // -- knx_list_comobjects --------------------------------------------------

    [Fact]
    public async Task ListComobjects()
    {
        await using var host = await McpTestHost.StartAsync();
        var cos = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        Assert.Equal(3, cos.Count);
        var refs = cos.StringsAt("ref");
        Assert.Contains("co-0001", refs);
        Assert.Contains("co-0002", refs);
        Assert.Contains("co-0003", refs);

        // co-0001 has a pre-existing link to ga-0001
        var co1 = cos.FindBy("ref", "co-0001");
        Assert.Contains("ga-0001", co1.ArrayAt("links").Strings());
    }

    [Fact]
    public async Task ListComobjectsUnknownDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-nonexistent")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_topology ---------------------------------------------------------

    [Fact]
    public async Task Topology()
    {
        await using var host = await McpTestHost.StartAsync();
        var areas = ToolAssert.Array(await host.CallAsync("knx_topology"));
        var area = Assert.Single(areas).AsObj();
        Assert.Equal("area-1", area.StringAt("areaRef"));
        Assert.Equal("Building A", area.StringAt("name"));

        // Lines
        var lines = area.ArrayAt("lines");
        Assert.Equal(2, lines.Count);
        var lineRefs = lines.StringsAt("lineRef");
        Assert.Contains("line-1.1", lineRefs);
        Assert.Contains("line-1.2", lineRefs);

        // Segments under line-1.1
        var mainLine = lines.FindBy("lineRef", "line-1.1");
        var segment = Assert.Single(mainLine.ArrayAt("segments"));
        Assert.Equal("seg-1.1.0", segment.StringAt("segmentRef"));
    }

    // -- knx_catalog_manufacturers --------------------------------------------

    [Fact]
    public async Task CatalogManufacturers()
    {
        await using var host = await McpTestHost.StartAsync();
        var mfrs = ToolAssert.Array(await host.CallAsync("knx_catalog_manufacturers"));
        Assert.Equal(2, mfrs.Count);
        var names = mfrs.StringsAt("name");
        Assert.Contains("ABB", names);
        Assert.Contains("Gira", names);
    }

    // -- knx_catalog_search ---------------------------------------------------

    [Fact]
    public async Task CatalogSearchByQuery()
    {
        // Search by query returns matching items.
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Actuator"))));
        Assert.Equal(2, items.Count);
        var names = items.StringsAt("name");
        Assert.Contains("Switch Actuator 4-fold", names);
        Assert.Contains("Dimming Actuator 2-fold", names);
    }

    [Fact]
    public async Task CatalogSearchByQueryNarrow()
    {
        // A more specific query returns fewer results.
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Dimming"))));
        var item = Assert.Single(items);
        Assert.Equal("cat-abb-da2", item.StringAt("catalogItemRef"));
    }

    [Fact]
    public async Task CatalogSearchByManufacturer()
    {
        // Filtering by manufacturerRef restricts results.
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(
            ("query", "Actuator"),
            ("manufacturer_ref", "mfr-gira"))));
        Assert.Empty(items);
    }

    [Fact]
    public async Task CatalogSearchByOrderNumber()
    {
        // Search matches on order number too.
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "2031"))));
        var item = Assert.Single(items);
        Assert.Equal("Tastsensor 3 Komfort", item.StringAt("name"));
    }

    // -- knx_list_parameters --------------------------------------------------

    [Fact]
    public async Task ListParameters()
    {
        // active_only defaults to True, so pass False here to also see the inactive param-0003.
        await using var host = await McpTestHost.StartAsync();
        var parameters = ToolAssert.Array(await host.CallAsync("knx_list_parameters", Args.Of(
            ("device_ref", "dev-0001"),
            ("active_only", false))));
        Assert.Equal(2, parameters.Count);
        var refs = parameters.StringsAt("parameterRef");
        Assert.Contains("param-0001", refs);
        Assert.Contains("param-0003", refs);

        // param-0001: value == default -> isDefault True
        var p1 = parameters.FindBy("parameterRef", "param-0001");
        Assert.Equal("normal", p1.StringAt("value"));
        Assert.True(p1.BoolAt("isDefault"));
        Assert.True(p1.BoolAt("isActive"));
        // Enum semantics: label + allowed choices so the LLM picks a valid value.
        Assert.Equal("Operating Mode", p1.StringAt("text"));
        Assert.Equal("ReadWrite", p1.StringAt("access"));
        var options = p1.ArrayAt("options");
        var optionValues = options.StringsAt("value").ToHashSet();
        Assert.True(
            optionValues.SetEquals(["normal", "night", "comfort"]),
            $"Unexpected option values: {string.Join(", ", optionValues)}");
        Assert.Contains(options.Objects(), o => o.StringAt("text") == "Comfort");
        // Not a numeric parameter -> no min/max.
        Assert.False(p1.ContainsKey("min"));
        Assert.False(p1.ContainsKey("max"));

        // param-0003: isActive False (deactivated by a controlling parameter), numeric range.
        var p3 = parameters.FindBy("parameterRef", "param-0003");
        Assert.False(p3.BoolAt("isActive"));
        Assert.Equal("Switch-on delay", p3.StringAt("text"));
        Assert.Equal("s", p3.StringAt("unit"));
        Assert.Equal("0", p3.StringAt("min"));
        Assert.Equal("255", p3.StringAt("max"));
        // Numeric parameter -> no enum options.
        Assert.False(p3.ContainsKey("options"));
    }

    [Fact]
    public async Task ListParametersNonDefault()
    {
        // Dimming Speed has a non-default value.
        await using var host = await McpTestHost.StartAsync();
        var parameters = ToolAssert.Array(await host.CallAsync("knx_list_parameters", Args.Of(("device_ref", "dev-0002"))));
        var p = Assert.Single(parameters);
        Assert.Equal("param-0002", p.StringAt("parameterRef"));
        Assert.Equal("3", p.StringAt("value"));
        Assert.False(p.BoolAt("isDefault"));
    }

    [Fact]
    public async Task ListParametersUnknownDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_list_parameters", Args.Of(("device_ref", "dev-nonexistent")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task ListParametersBlockPath()
    {
        // Parameters carry their UI block path to disambiguate repeated names.
        await using var host = await McpTestHost.StartAsync();
        var parameters = ToolAssert.Array(await host.CallAsync("knx_list_parameters", Args.Of(
            ("device_ref", "dev-0001"),
            ("active_only", false))));
        var p3 = parameters.FindBy("parameterRef", "param-0003");
        Assert.Contains("PB9/10", p3.StringAt("block"));
        // Targeting workflow: block + name yields exactly one instance.
        var hits = parameters.Objects()
            .Where(p => (p.OptStringAt("block") ?? "").Contains("PB9/10", StringComparison.Ordinal)
                        && p.StringAt("name") == "Switch-on delay")
            .ToList();
        var hit = Assert.Single(hits);
        Assert.Equal("param-0003", hit.StringAt("parameterRef"));
    }

    [Fact]
    public async Task ListParametersActiveOnlyDefault()
    {
        // active_only defaults to True -> the inactive param-0003 is dropped by default.
        await using var host = await McpTestHost.StartAsync();
        var parameters = ToolAssert.Array(await host.CallAsync("knx_list_parameters", Args.Of(("device_ref", "dev-0001"))));
        var refs = parameters.StringsAt("parameterRef");
        Assert.Contains("param-0001", refs);        // active
        Assert.DoesNotContain("param-0003", refs);  // inactive -> filtered by default
        Assert.All(parameters.Objects(), p => Assert.True(p.BoolAt("isActive")));
    }

    [Fact]
    public async Task ListDevicesNameContainsMatchesDescription()
    {
        // Finding a device by its human label in `description` (name is the product name).
        await using var host = await McpTestHost.StartAsync();
        var devs = ToolAssert.Array(await host.CallAsync("knx_list_devices", Args.Of(("name_contains", "couch"))));
        Assert.Contains(devs.Objects(), d => d.StringAt("ref") == "dev-0001");  // matched via description "Couch"
        Assert.All(devs.Objects(), d =>
            Assert.DoesNotContain("couch", (d.OptStringAt("name") ?? "").ToLowerInvariant()));  // not via name
    }

    [Fact]
    public async Task ListGroupAddressesNameContainsMatchesDescription()
    {
        // Finding a GA by a label in `description` (name is a scheme like 'Light Living Room').
        await using var host = await McpTestHost.StartAsync();
        var gas = ToolAssert.Array(await host.CallAsync("knx_list_group_addresses", Args.Of(("name_contains", "mittelgang"))));
        Assert.Contains(gas.Objects(), g => g.StringAt("ref") == "ga-0001");  // matched via description "Mittelgang"
    }

    [Fact]
    public async Task ListParametersActiveOnly()
    {
        // active_only drops inactive parameters (param-0003 is inactive).
        await using var host = await McpTestHost.StartAsync();
        var parameters = ToolAssert.Array(await host.CallAsync("knx_list_parameters", Args.Of(
            ("device_ref", "dev-0001"),
            ("active_only", true))));
        var refs = parameters.StringsAt("parameterRef");
        Assert.Contains("param-0001", refs);        // active
        Assert.DoesNotContain("param-0003", refs);  // inactive -> filtered out
        Assert.All(parameters.Objects(), p => Assert.True(p.BoolAt("isActive")));
    }

    [Fact]
    public async Task ListParametersNameContains()
    {
        // name_contains filters case-insensitively on name/label.
        // param-0003 ("Switch-on delay") is inactive, so active_only=False is needed to reach it.
        await using var host = await McpTestHost.StartAsync();
        var parameters = ToolAssert.Array(await host.CallAsync("knx_list_parameters", Args.Of(
            ("device_ref", "dev-0001"),
            ("name_contains", "delay"),
            ("active_only", false))));
        var p = Assert.Single(parameters);
        Assert.Equal("param-0003", p.StringAt("parameterRef"));  // "Switch-on delay"
    }

    [Fact]
    public async Task ListComobjectsNameContains()
    {
        // name_contains scopes a large ComObject list to matching entries.
        await using var host = await McpTestHost.StartAsync();
        var full = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0002"))));
        var filtered = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(
            ("device_ref", "dev-0002"),
            ("name_contains", "dim object b"))));
        Assert.True(filtered.Count < full.Count, $"filtered={filtered.Count} full={full.Count}");
        Assert.All(filtered.Objects(), c =>
            Assert.Contains("dim object b", (c.OptStringAt("name") ?? "").ToLowerInvariant()));
    }

    [Fact]
    public async Task ListComobjectsChannelGrouping()
    {
        // ComObjects carry a channel label; dev-0001 objects are channel-independent.
        await using var host = await McpTestHost.StartAsync();
        var cos2 = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0002"))));
        Assert.Equal("Channel A - Dimming", cos2.FindBy("ref", "co-0004").StringAt("channel"));
        Assert.Equal("Channel B - Dimming", cos2.FindBy("ref", "co-0005").StringAt("channel"));

        // dev-0001 objects have no channel (channel-independent) -> field omitted.
        var cos1 = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        Assert.All(cos1.Objects(), c => Assert.False(c.ContainsKey("channel")));
    }

    [Fact]
    public async Task ListComobjectsBlockPath()
    {
        // ComObjects carry the authoritative ETS block path (`block`).
        await using var host = await McpTestHost.StartAsync();
        var cos = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0002"))));
        Assert.Equal("Operation / Display > Dimming > Channel A", cos.FindBy("ref", "co-0004").StringAt("block"));
    }

    [Fact]
    public async Task ApplicationDynamic()
    {
        // application.dynamic returns the dynamic UI tree XML.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_application_dynamic", Args.Of(("device_ref", "dev-0002"))));
        Assert.True(data.ContainsKey("xml"));
        var xml = data.StringAt("xml");
        Assert.Contains("ParameterBlock", xml);
        Assert.Contains("ParameterRefRef", xml);
    }

    [Fact]
    public async Task ApplicationDynamicUnknownDevice()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_application_dynamic", Args.Of(("device_ref", "dev-nope")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task ListComobjectsFilterByChannel()
    {
        // name_contains matches the channel label, scoping to one function.
        await using var host = await McpTestHost.StartAsync();
        var result = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(
            ("device_ref", "dev-0002"),
            ("name_contains", "channel a"))));
        var refs = result.StringsAt("ref").ToHashSet();
        Assert.Equal("co-0004", Assert.Single(refs));
    }
}
