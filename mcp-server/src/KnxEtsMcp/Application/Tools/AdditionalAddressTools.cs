// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Additional address tools.</summary>
[McpServerToolType]
public sealed class AdditionalAddressTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_add_device_additional_address", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Add an additional individual address to a device.
        """)]
    public async Task<JsonNode?> AddDeviceAdditionalAddress(
        string device_ref,
        int address,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("address", address);
        var result = await bridge.CallAsync("device.addAdditionalAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_remove_device_additional_address", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Remove an additional individual address from a device.
        """)]
    public async Task<JsonNode?> RemoveDeviceAdditionalAddress(
        string device_ref,
        int address,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("address", address);
        var result = await bridge.CallAsync("device.removeAdditionalAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_add_line_additional_ga", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Add additional group addresses to a coupler/line filter table.
        """)]
    public async Task<JsonNode?> AddLineAdditionalGa(
        string line_ref,
        string[] group_address_refs,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref)
            .Set("groupAddressRefs", group_address_refs);
        var result = await bridge.CallAsync("line.addAdditionalGroupAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_remove_line_additional_ga", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Remove additional group addresses from a coupler/line filter table.
        """)]
    public async Task<JsonNode?> RemoveLineAdditionalGa(
        string line_ref,
        string[] group_address_refs,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref)
            .Set("groupAddressRefs", group_address_refs);
        var result = await bridge.CallAsync("line.removeAdditionalGroupAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
