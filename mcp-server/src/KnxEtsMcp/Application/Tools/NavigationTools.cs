// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Navigation tool.</summary>
[McpServerToolType]
public sealed class NavigationTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_navigate_to", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Navigate the ETS UI to a specific object (device, GA, line, building part, etc.).
        """)]
    public async Task<JsonNode?> NavigateTo(
        string @ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("ref", @ref);
        var result = await bridge.CallAsync("project.navigateTo", parameters, null, cancellationToken);
        return result;
    }
}
