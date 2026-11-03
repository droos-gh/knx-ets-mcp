// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Label setter tools (v0.1.16).</summary>
[McpServerToolType]
public sealed class LabelSetterTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_set_device_description", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set a device's description field.
        """)]
    public async Task<JsonNode?> SetDeviceDescription(
        string device_ref,
        string description,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("description", description);
        var result = await bridge.CallAsync("device.setDescription", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_device_comment", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set a device's comment field.
        """)]
    public async Task<JsonNode?> SetDeviceComment(
        string device_ref,
        string comment,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("comment", comment);
        var result = await bridge.CallAsync("device.setComment", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_comobject_description", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set a communication object's description field.
        """)]
    public async Task<JsonNode?> SetComobjectDescription(
        string com_object_ref,
        string description,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("comObjectRef", com_object_ref)
            .Set("description", description);
        var result = await bridge.CallAsync("comObject.setDescription", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_comobject_function_text", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set a communication object's functionText field.
        """)]
    public async Task<JsonNode?> SetComobjectFunctionText(
        string com_object_ref,
        string function_text,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("comObjectRef", com_object_ref)
            .Set("functionText", function_text);
        var result = await bridge.CallAsync("comObject.setFunctionText", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
