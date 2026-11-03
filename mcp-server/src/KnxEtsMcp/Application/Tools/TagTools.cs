// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Tag tools.</summary>
[McpServerToolType]
public sealed class TagTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_list_tags", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List all tags defined in the KNX project.
        """)]
    public async Task<JsonNode?> ListTags(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("tags.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_create_tag", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a tag in the KNX project.
        """)]
    public async Task<JsonNode?> CreateTag(
        string label,
        string? color = null,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("label", label)
            .SetOptional("color", color);
        var result = await bridge.CallAsync("tag.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_tag", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a tag from the KNX project.
        """)]
    public async Task<JsonNode?> DeleteTag(
        string tag_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("tagRef", tag_ref);
        var result = await bridge.CallAsync("tag.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
