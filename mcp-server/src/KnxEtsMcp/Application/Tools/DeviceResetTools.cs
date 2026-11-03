// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Device reset (bus write).</summary>
[McpServerToolType]
public sealed class DeviceResetTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_device_reset", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Send a restart command to a physical KNX device on the bus.
        """)]
    public async Task<JsonNode?> DeviceReset(
        string device_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("device.reset", parameters, null, cancellationToken);
        return result;
    }
}
