// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Channel / module tools.</summary>
[McpServerToolType]
public sealed class ChannelModuleTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_device_channels", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List channels and modules for a device.
        """)]
    public async Task<JsonNode?> DeviceChannels(
        string device_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("device.channels", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_application_dynamic", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Get the device application program's DYNAMIC UI tree as XML: {xml}.

        This is the authoritative structure ETS uses to render the parameter view --
        ParameterBlock / Channel nodes (with Name/Text like "PB9/10: Push buttons 9/10") and
        ParameterRefRef entries (RefId pointing at a parameter). Use it to reliably determine
        which UI block/channel a parameter belongs to on complex multi-function devices, where
        the same parameter name (e.g. "Key label for left push button") occurs many times: find
        the block by Text, then the ParameterRefRef under it whose RefId matches the target, and
        set that exact parameterRef. The SDK object model does NOT otherwise expose a
        parameter's block, so for per-block parameter targeting this is the reliable source.

        Args:
            device_ref: The ref of the device (from knx_list_devices).
        """)]
    public async Task<JsonNode?> ApplicationDynamic(
        [Description("The ref of the device (from knx_list_devices).")] string device_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("application.dynamic", parameters, null, cancellationToken);
        return result;
    }
}
