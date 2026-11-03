// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Bus interface / filter tools.</summary>
[McpServerToolType]
public sealed class BusInterfaceFilterTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_bus_interface_link", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Link group addresses to a coupler's bus-interface filter.
        """)]
    public async Task<JsonNode?> BusInterfaceLink(
        string device_ref,
        string[] group_address_refs,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("groupAddressRefs", group_address_refs);
        var result = await bridge.CallAsync("busInterface.link", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_bus_interface_unlink", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Unlink group addresses from a coupler's bus-interface filter.
        """)]
    public async Task<JsonNode?> BusInterfaceUnlink(
        string device_ref,
        string[] group_address_refs,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("groupAddressRefs", group_address_refs);
        var result = await bridge.CallAsync("busInterface.unlink", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
