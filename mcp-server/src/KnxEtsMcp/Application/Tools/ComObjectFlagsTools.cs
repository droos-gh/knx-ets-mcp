// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase A: ComObject flags tool.</summary>
[McpServerToolType]
public sealed class ComObjectFlagsTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_set_comobject_flags", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set communication flags (C/R/W/T/U) on a communication object.

        Typical patterns: a COMMAND/input object receives -> Communication+Write (often +Update);
        a STATUS/output object sends -> Communication+Read+Transmit. Only change flags when you
        have a reason -- the product's defaults are usually correct; forcing flags can break the
        device's intended behavior. Only provided flags are changed; others keep their current
        value. The CO must be active (IsActive guard). Returns read-back flag state.

        Args:
            com_object_ref: The ref of the communication object.
            communication_flag: Enable/disable communication. Optional.
            read_flag: Enable/disable read. Optional.
            write_flag: Enable/disable write. Optional.
            transmit_flag: Enable/disable transmit. Optional.
            update_flag: Enable/disable update. Optional.
            read_on_init_flag: Enable/disable read-on-init. Optional.
            priority: Priority level: "Low", "High", or "Alert". Optional.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> SetComobjectFlags(
        [Description("The ref of the communication object.")] string com_object_ref,
        [Description("Enable/disable communication. Optional.")] bool? communication_flag = null,
        [Description("Enable/disable read. Optional.")] bool? read_flag = null,
        [Description("Enable/disable write. Optional.")] bool? write_flag = null,
        [Description("Enable/disable transmit. Optional.")] bool? transmit_flag = null,
        [Description("Enable/disable update. Optional.")] bool? update_flag = null,
        [Description("Enable/disable read-on-init. Optional.")] bool? read_on_init_flag = null,
        [Description("Priority level: \"Low\", \"High\", or \"Alert\". Optional.")] string? priority = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("comObjectRef", com_object_ref)
            .SetOptional("communicationFlag", communication_flag)
            .SetOptional("readFlag", read_flag)
            .SetOptional("writeFlag", write_flag)
            .SetOptional("transmitFlag", transmit_flag)
            .SetOptional("updateFlag", update_flag)
            .SetOptional("readOnInitFlag", read_on_init_flag)
            .SetOptional("priority", priority);
        var result = await bridge.CallAsync("comObject.setFlags", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
