// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: Trade tools.</summary>
[McpServerToolType]
public sealed class TradeTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_list_trades", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List all trades (Gewerke) in the KNX project.
        """)]
    public async Task<JsonNode?> ListTrades(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("trades.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_create_trade", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a new trade (Gewerk) in the KNX project.
        """)]
    public async Task<JsonNode?> CreateTrade(
        string name,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("name", name);
        var result = await bridge.CallAsync("trade.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_trade", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a trade from the KNX project.
        """)]
    public async Task<JsonNode?> DeleteTrade(
        string trade_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("tradeRef", trade_ref);
        var result = await bridge.CallAsync("trade.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_trade_assign_device", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Assign a device to a trade.
        """)]
    public async Task<JsonNode?> TradeAssignDevice(
        string trade_ref,
        string device_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("tradeRef", trade_ref)
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("trade.assignDevice", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_trade_unassign_device", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Unassign a device from a trade.
        """)]
    public async Task<JsonNode?> TradeUnassignDevice(
        string trade_ref,
        string device_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("tradeRef", trade_ref)
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("trade.unassignDevice", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
