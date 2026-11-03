// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Parameter default tool.</summary>
[McpServerToolType]
public sealed class ParameterDefaultTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_set_parameter_default", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Reset a device parameter to its default value.
        """)]
    public async Task<JsonNode?> SetParameterDefault(
        string device_ref,
        string parameter_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("parameterRef", parameter_ref);
        var result = await bridge.CallAsync("param.setDefault", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
