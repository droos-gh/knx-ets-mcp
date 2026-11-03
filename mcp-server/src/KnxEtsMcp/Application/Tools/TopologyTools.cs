// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase A: Topology tools.</summary>
[McpServerToolType]
public sealed class TopologyTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_create_area", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a new area in the KNX topology.

        Args:
            name: Name for the new area.
            address: Area address (0-15). Auto-allocated if omitted.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> CreateArea(
        [Description("Name for the new area.")] string name,
        [Description("Area address (0-15). Auto-allocated if omitted.")] int? address = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("name", name)
            .SetOptional("address", address);
        var result = await bridge.CallAsync("area.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_area", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete an area from the KNX topology.

        Args:
            area_ref: The ref of the area to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteArea(
        [Description("The ref of the area to delete.")] string area_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("areaRef", area_ref);
        var result = await bridge.CallAsync("area.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_create_line", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a new line under an area in the KNX topology.

        Args:
            area_ref: The ref of the parent area.
            name: Name for the new line.
            address: Line address (0-15). Auto-allocated if omitted.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> CreateLine(
        [Description("The ref of the parent area.")] string area_ref,
        [Description("Name for the new line.")] string name,
        [Description("Line address (0-15). Auto-allocated if omitted.")] int? address = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("areaRef", area_ref)
            .Set("name", name)
            .SetOptional("address", address);
        var result = await bridge.CallAsync("line.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_line", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a line from the KNX topology.

        Args:
            line_ref: The ref of the line to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteLine(
        [Description("The ref of the line to delete.")] string line_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref);
        var result = await bridge.CallAsync("line.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
