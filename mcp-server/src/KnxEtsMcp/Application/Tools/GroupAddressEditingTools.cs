// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase A: Group Address editing tools.</summary>
[McpServerToolType]
public sealed class GroupAddressEditingTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_delete_group_address", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a group address from the KNX project.

        Args:
            ga_ref: The ref of the group address to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteGroupAddress(
        [Description("The ref of the group address to delete.")] string ga_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("groupAddressRef", ga_ref);
        var result = await bridge.CallAsync("ga.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_rename_group_address", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Rename a group address.

        Args:
            ga_ref: The ref of the group address.
            name: The new name.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> RenameGroupAddress(
        [Description("The ref of the group address.")] string ga_ref,
        [Description("The new name.")] string name,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("groupAddressRef", ga_ref)
            .Set("name", name);
        var result = await bridge.CallAsync("ga.rename", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_group_address_description", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set the description of a group address.

        Args:
            ga_ref: The ref of the group address.
            description: The new description text.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> SetGroupAddressDescription(
        [Description("The ref of the group address.")] string ga_ref,
        [Description("The new description text.")] string description,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("groupAddressRef", ga_ref)
            .Set("description", description);
        var result = await bridge.CallAsync("ga.setDescription", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_group_address_dpt", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set the datapoint type of a group address.

        Args:
            ga_ref: The ref of the group address.
            dpt_main: Main DPT number (e.g. 1 for switching).
            dpt_sub: Sub DPT number (e.g. 1 for DPT 1.001). Optional.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> SetGroupAddressDpt(
        [Description("The ref of the group address.")] string ga_ref,
        [Description("Main DPT number (e.g. 1 for switching).")] int dpt_main,
        [Description("Sub DPT number (e.g. 1 for DPT 1.001). Optional.")] int? dpt_sub = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("groupAddressRef", ga_ref)
            .Set("dptMain", dpt_main)
            .SetOptional("dptSub", dpt_sub);
        var result = await bridge.CallAsync("ga.setDatapointType", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
