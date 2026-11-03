using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Port of test_catalog_online.py: online catalog tools -- search_online, import, internalize.</summary>
public sealed class CatalogOnlineTests
{
    private const string SensorPath = "C:\\Products\\sensor.knxprod";

    // -- knx_search_online_catalog ------------------------------------------------

    // Broad query returns all matching unified catalog items ("ABB" is not in
    // name/orderNumber, so only the result type is checked).
    [Fact]
    public async Task TestSearchOnlineReturnsUnifiedItems()
    {
        await using var host = await McpTestHost.StartAsync();
        ToolAssert.Array(await host.CallAsync("knx_search_online_catalog", Args.Of(("query", "ABB"))));
    }

    // Query matching product name returns the right item.
    [Fact]
    public async Task TestSearchOnlineByName()
    {
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_search_online_catalog", Args.Of(("query", "Binary"))));
        var item = Assert.Single(items).ObjB();
        Assert.Equal("uci:gira-be2", item["unifiedCatalogItemRef"].StrB());
        Assert.Equal("Gira", item["manufacturer"].StrB());
        Assert.Equal("Binary Input 2-fold", item["name"].StrB());
        Assert.Equal("1120 00", item["orderNumber"].StrB());
    }

    [Fact]
    public async Task TestSearchOnlineByOrderNumber()
    {
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_search_online_catalog", Args.Of(("query", "ROG"))));
        var item = Assert.Single(items).ObjB();
        Assert.Equal("uci:abb-rog", item["unifiedCatalogItemRef"].StrB());
    }

    // manufacturerRef filters results to that manufacturer only
    // ("Input" only matches Gira's Binary Input; filtered to ABB -> empty).
    [Fact]
    public async Task TestSearchOnlineFilterByManufacturer()
    {
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_search_online_catalog", Args.Of(
            ("query", "Input"),
            ("manufacturer_ref", "mfr-abb"))));
        Assert.Empty(items);
    }

    // A query with no match returns an empty list (not an error).
    [Fact]
    public async Task TestSearchOnlineNoMatch()
    {
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_search_online_catalog", Args.Of(("query", "nonexistent-xyz"))));
        Assert.Empty(items);
    }

    // Online items must NOT appear in the local catalog search.
    [Fact]
    public async Task TestSearchOnlineItemsNotInLocalCatalog()
    {
        await using var host = await McpTestHost.StartAsync();
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Binary"))));
        Assert.Empty(items); // "Binary Input 2-fold" is online-only
    }

    // -- knx_import_product -------------------------------------------------------

    // Importing a valid .knxprod path returns imported items.
    [Fact]
    public async Task TestImportValidPath()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_import_product", Args.Of(("path", SensorPath))));
        Assert.True(data.HasB("imported"));
        var item = Assert.Single(data["imported"].ArrB()).ObjB();
        Assert.Equal("cat-imp-sensor", item["catalogItemRef"].StrB());
        Assert.Equal("Imported Sensor Module", item["name"].StrB());
    }

    // After import, the product is findable via local catalog search.
    [Fact]
    public async Task TestImportItemAppearsInLocalSearch()
    {
        await using var host = await McpTestHost.StartAsync();
        // Import first
        await host.CallAsync("knx_import_product", Args.Of(("path", SensorPath)));
        // Now search locally
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Imported Sensor"))));
        var item = Assert.Single(items).ObjB();
        Assert.Equal("cat-imp-sensor", item["catalogItemRef"].StrB());
    }

    // Importing a nonexistent path returns an error.
    [Fact]
    public async Task TestImportInvalidPath()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_import_product", Args.Of(("path", "C:\\bad\\missing.knxprod")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // Importing the same path twice does not double-add items.
    [Fact]
    public async Task TestImportIdempotent()
    {
        await using var host = await McpTestHost.StartAsync();
        for (var i = 0; i < 2; i++)
        {
            var data = ToolAssert.Object(await host.CallAsync("knx_import_product", Args.Of(("path", SensorPath))));
            Assert.Single(data["imported"].ArrB());
        }

        // Local search should return exactly one item, not two.
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Imported Sensor"))));
        Assert.Single(items);
    }

    // -- knx_internalize_product --------------------------------------------------

    // Internalizing a known unified ref returns a local ci: ref.
    [Fact]
    public async Task TestInternalizeKnownRef()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_internalize_product", Args.Of(
            ("unified_catalog_item_ref", "uci:gira-be2"))));
        Assert.True(data.HasB("catalogItemRef"));
        Assert.StartsWith("ci:", data["catalogItemRef"].StrB(), StringComparison.Ordinal);
    }

    // After internalize, the item is findable in the local catalog.
    [Fact]
    public async Task TestInternalizeItemAppearsInLocalSearch()
    {
        await using var host = await McpTestHost.StartAsync();
        // Internalize
        await host.CallAsync("knx_internalize_product", Args.Of(("unified_catalog_item_ref", "uci:gira-be2")));
        // Search locally -- "Binary Input" was online-only before internalize.
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Binary Input"))));
        var item = Assert.Single(items).ObjB();
        Assert.Equal("Binary Input 2-fold", item["name"].StrB());
        Assert.StartsWith("ci:", item["catalogItemRef"].StrB(), StringComparison.Ordinal);
    }

    // Internalizing an unknown unified ref returns not_found.
    [Fact]
    public async Task TestInternalizeUnknownRef()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_internalize_product", Args.Of(("unified_catalog_item_ref", "uci:unknown-xyz")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // Internalizing the same ref twice does not double-add in local catalog.
    [Fact]
    public async Task TestInternalizeIdempotent()
    {
        await using var host = await McpTestHost.StartAsync();
        for (var i = 0; i < 2; i++)
        {
            var data = ToolAssert.Object(await host.CallAsync("knx_internalize_product", Args.Of(
                ("unified_catalog_item_ref", "uci:abb-spau"))));
            Assert.StartsWith("ci:", data["catalogItemRef"].StrB(), StringComparison.Ordinal);
        }

        // Local search should return exactly one match.
        var items = ToolAssert.Array(await host.CallAsync("knx_catalog_search", Args.Of(("query", "Space Unit"))));
        Assert.Single(items);
    }
}
