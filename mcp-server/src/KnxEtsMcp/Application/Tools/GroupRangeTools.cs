// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase A: Group Range tools.</summary>
[McpServerToolType]
public sealed class GroupRangeTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_create_group_range", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a group range (main/middle group) in the group address structure.

        Args:
            name: Name for the new group range.
            address: Address number for this level (0-31 for main groups, 0-7 for middle).
            parent_group_range_ref: Parent group range ref for nested ranges. Omit for top-level.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> CreateGroupRange(
        [Description("Name for the new group range.")] string name,
        [Description("Address number for this level (0-31 for main groups, 0-7 for middle).")] int address,
        [Description("Parent group range ref for nested ranges. Omit for top-level.")] string? parent_group_range_ref = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("name", name)
            .Set("address", address)
            .SetOptional("parentGroupRangeRef", parent_group_range_ref);
        var result = await bridge.CallAsync("groupRange.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_group_range", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a group range and all its contents.

        Args:
            group_range_ref: The ref of the group range to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteGroupRange(
        [Description("The ref of the group range to delete.")] string group_range_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("groupRangeRef", group_range_ref);
        var result = await bridge.CallAsync("groupRange.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
