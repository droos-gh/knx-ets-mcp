// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Segment tools.</summary>
[McpServerToolType]
public sealed class SegmentTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_create_segment", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a segment under a topology line.
        """)]
    public async Task<JsonNode?> CreateSegment(
        string line_ref,
        string name,
        string medium_type,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref)
            .Set("name", name)
            .Set("mediumType", medium_type);
        var result = await bridge.CallAsync("segment.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_segment", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a segment from a topology line.
        """)]
    public async Task<JsonNode?> DeleteSegment(
        string segment_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("segmentRef", segment_ref);
        var result = await bridge.CallAsync("segment.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
