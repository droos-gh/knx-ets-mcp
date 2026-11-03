// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Move tools.</summary>
[McpServerToolType]
public sealed class MoveTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_move_group_address", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Move a group address to a different group range.
        """)]
    public async Task<JsonNode?> MoveGroupAddress(
        string target_group_range_ref,
        string group_address_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("targetGroupRangeRef", target_group_range_ref)
            .Set("groupAddressRef", group_address_ref);
        var result = await bridge.CallAsync("groupRange.moveGroupAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_move_group_range", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Move a group range under a different parent group range.
        """)]
    public async Task<JsonNode?> MoveGroupRange(
        string target_group_range_ref,
        string source_group_range_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("targetGroupRangeRef", target_group_range_ref)
            .Set("sourceGroupRangeRef", source_group_range_ref);
        var result = await bridge.CallAsync("groupRange.moveGroupRange", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_move_line", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Move a topology line to a different area.
        """)]
    public async Task<JsonNode?> MoveLine(
        string target_area_ref,
        string line_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("targetAreaRef", target_area_ref)
            .Set("lineRef", line_ref);
        var result = await bridge.CallAsync("line.move", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_move_building_part", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Move a building part under a different parent building part.
        """)]
    public async Task<JsonNode?> MoveBuildingPart(
        string target_building_part_ref,
        string source_building_part_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("targetBuildingPartRef", target_building_part_ref)
            .Set("sourceBuildingPartRef", source_building_part_ref);
        var result = await bridge.CallAsync("buildingPart.move", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
