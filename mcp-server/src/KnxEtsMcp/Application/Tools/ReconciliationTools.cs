// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Reconciliation tools.</summary>
[McpServerToolType]
public sealed class ReconciliationTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_device_unassign", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Detach a device from its line without deleting it.

        The device is moved to the unassigned-devices collection and can be
        reassigned to a line later.  MUTATING: requires idempotency key.

        Args:
            device_ref: The ref of the device to unassign.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeviceUnassign(
        [Description("The ref of the device to unassign.")] string device_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("device.unassign", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
