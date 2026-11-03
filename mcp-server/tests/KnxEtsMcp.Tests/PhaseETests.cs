using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Phase E: catalog browsing tools (port of tests/test_phase_e.py).</summary>
public sealed class PhaseETests
{
    // -- knx_browse_products --------------------------------------------------

    [Fact]
    public async Task BrowseProducts()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_browse_products", Args.Of(("manufacturer_ref", "mfr-abb"))));
        Assert.Equal(2, data.Count);  // ABB has 2 catalog items
        var names = data.StringsAt("name");
        Assert.Contains("Switch Actuator 4-fold", names);
        Assert.Contains("Dimming Actuator 2-fold", names);
    }

    [Fact]
    public async Task BrowseProductsPagination()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_browse_products", Args.Of(
            ("manufacturer_ref", "mfr-abb"),
            ("offset", 0),
            ("limit", 1))));
        Assert.Single(data);
    }

    [Fact]
    public async Task BrowseProductsOffsetPastEnd()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_browse_products", Args.Of(
            ("manufacturer_ref", "mfr-abb"),
            ("offset", 100))));
        Assert.Empty(data);
    }

    [Fact]
    public async Task BrowseProductsGira()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Array(await host.CallAsync("knx_browse_products", Args.Of(("manufacturer_ref", "mfr-gira"))));
        var item = Assert.Single(data);
        Assert.Equal("Tastsensor 3 Komfort", item.StringAt("name"));
    }

    [Fact]
    public async Task BrowseProductsManufacturerNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_browse_products", Args.Of(("manufacturer_ref", "mfr-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_product_info -----------------------------------------------------

    [Fact]
    public async Task ProductInfo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_product_info", Args.Of(("catalog_item_ref", "cat-abb-sa4"))));
        Assert.Equal("cat-abb-sa4", data.StringAt("catalogItemRef"));
        Assert.Equal("ABB", data.StringAt("manufacturer"));
        Assert.Equal("Switch Actuator 4-fold", data.StringAt("name"));
        Assert.Equal("SA/S 4.16.1", data.StringAt("orderNumber"));
        Assert.True(data.ContainsKey("description"));
        Assert.True(data.ContainsKey("mediumTypes"));
    }

    [Fact]
    public async Task ProductInfoNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_product_info", Args.Of(("catalog_item_ref", "cat-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }
}
