// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase B: Building function tools.</summary>
[McpServerToolType]
public sealed class BuildingFunctionTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_create_building_function", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a building function (collects group addresses in building structure).

        Args:
            building_part_ref: The ref of the parent building part.
            name: Name for the building function.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> CreateBuildingFunction(
        [Description("The ref of the parent building part.")] string building_part_ref,
        [Description("Name for the building function.")] string name,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingPartRef", building_part_ref)
            .Set("name", name);
        var result = await bridge.CallAsync("buildingFunction.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_building_function", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a building function.

        Args:
            building_function_ref: The ref of the building function to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteBuildingFunction(
        [Description("The ref of the building function to delete.")] string building_function_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingFunctionRef", building_function_ref);
        var result = await bridge.CallAsync("buildingFunction.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_link_building_function_ga", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Link group addresses to a building function.

        Args:
            building_function_ref: The ref of the building function.
            group_address_refs: List of group address refs to link.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> LinkBuildingFunctionGa(
        [Description("The ref of the building function.")] string building_function_ref,
        [Description("List of group address refs to link.")] string[] group_address_refs,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingFunctionRef", building_function_ref)
            .Set("groupAddressRefs", group_address_refs);
        var result = await bridge.CallAsync("buildingFunction.linkGroupAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_unlink_building_function_ga", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Unlink group addresses from a building function.

        Args:
            building_function_ref: The ref of the building function.
            group_address_refs: List of group address refs to unlink.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> UnlinkBuildingFunctionGa(
        [Description("The ref of the building function.")] string building_function_ref,
        [Description("List of group address refs to unlink.")] string[] group_address_refs,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingFunctionRef", building_function_ref)
            .Set("groupAddressRefs", group_address_refs);
        var result = await bridge.CallAsync("buildingFunction.unlinkGroupAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
