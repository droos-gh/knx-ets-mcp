// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Project history tools.</summary>
[McpServerToolType]
public sealed class ProjectHistoryTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_list_project_history", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List project history entries (audit log).
        """)]
    public async Task<JsonNode?> ListProjectHistory(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("projectHistory.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_add_project_history", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Add an entry to the project history (audit log).
        """)]
    public async Task<JsonNode?> AddProjectHistory(
        string text,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("text", text);
        var result = await bridge.CallAsync("projectHistory.add", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_project_history", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a project history entry.
        """)]
    public async Task<JsonNode?> DeleteProjectHistory(
        string history_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("historyRef", history_ref);
        var result = await bridge.CallAsync("projectHistory.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
