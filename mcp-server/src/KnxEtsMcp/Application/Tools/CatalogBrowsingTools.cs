// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase E: Catalog browsing tools.</summary>
[McpServerToolType]
public sealed class CatalogBrowsingTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_browse_products", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Browse products in the catalog for a specific manufacturer with paging.

        Args:
            manufacturer_ref: Manufacturer ref (from knx_catalog_manufacturers).
            offset: Number of items to skip (default 0).
            limit: Maximum number of items to return (default 100).
        """)]
    public async Task<JsonNode?> BrowseProducts(
        [Description("Manufacturer ref (from knx_catalog_manufacturers).")] string manufacturer_ref,
        [Description("Number of items to skip (default 0).")] int offset = 0,
        [Description("Maximum number of items to return (default 100).")] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("manufacturerRef", manufacturer_ref)
            .Set("offset", offset)
            .Set("limit", limit);
        var result = await bridge.CallAsync("catalog.browseProducts", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_product_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Get detailed product information for a catalog item.

        Args:
            catalog_item_ref: The catalog item ref (from browse or search).
        """)]
    public async Task<JsonNode?> ProductInfo(
        [Description("The catalog item ref (from browse or search).")] string catalog_item_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("catalogItemRef", catalog_item_ref);
        var result = await bridge.CallAsync("catalog.productInfo", parameters, null, cancellationToken);
        return result;
    }
}
