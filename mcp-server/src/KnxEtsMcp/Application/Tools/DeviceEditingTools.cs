// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase A: Device editing tools.</summary>
[McpServerToolType]
public sealed class DeviceEditingTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_delete_device", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a device from the KNX project.

        Args:
            device_ref: The ref of the device to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteDevice(
        [Description("The ref of the device to delete.")] string device_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("device.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_rename_device", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Rename a device in the KNX project.

        Args:
            device_ref: The ref of the device.
            name: The new name.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> RenameDevice(
        [Description("The ref of the device.")] string device_ref,
        [Description("The new name.")] string name,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("name", name);
        var result = await bridge.CallAsync("device.rename", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_device_address", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set the physical address of a device (project model, not bus programming).

        Args:
            device_ref: The ref of the device.
            address: New individual address as "area.line.device" (e.g. "1.1.5").
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> SetDeviceAddress(
        [Description("The ref of the device.")] string device_ref,
        [Description("New individual address as \"area.line.device\" (e.g. \"1.1.5\").")] string address,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("address", address);
        var result = await bridge.CallAsync("device.setAddress", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_move_device", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Move a device to a different line. NOT SUPPORTED in ETS 6.3.0 SDK.

        This always returns a not_supported error. Workaround: delete and re-add.

        Args:
            device_ref: The ref of the device.
            line_ref: The ref of the target line.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> MoveDevice(
        [Description("The ref of the device.")] string device_ref,
        [Description("The ref of the target line.")] string line_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("lineRef", line_ref);
        var result = await bridge.CallAsync("device.move", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
