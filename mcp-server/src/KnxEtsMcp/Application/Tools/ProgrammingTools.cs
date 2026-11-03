// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Programming tools.</summary>
[McpServerToolType]
public sealed class ProgrammingTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_program_device", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Program (download application/parameters/GA tables to) a physical KNX device.

        This writes to the KNX bus but is reversible (re-programmable).  The
        operation is automatic and idempotent -- no approval token is needed.

        Args:
            device_ref: The ref of the device to program.
            options: Download type. "partial" (default) writes only the changes
                (parameters / GA tables) with NO individual-address step (no physical
                programming-button press) -- the safe everyday "apply my edits" download.
                "all" is a full download incl. individual address. Also "application",
                "network", "networkBySerial". Maps to ETS LoadDeviceOptions.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> ProgramDevice(
        [Description("The ref of the device to program.")] string device_ref,
        [Description("Download type. \"partial\" (default) writes only the changes (parameters / GA tables) with NO individual-address step (no physical programming-button press) -- the safe everyday \"apply my edits\" download. \"all\" is a full download incl. individual address. Also \"application\", \"network\", \"networkBySerial\". Maps to ETS LoadDeviceOptions.")] string? options = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .SetOptional("options", options);
        var result = await bridge.CallAsync("device.program", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_update_firmware", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Flash firmware onto a physical KNX device.

        WARNING: This is potentially irreversible.  A failed or wrong firmware
        update can brick the device.  The operation is gated by the AddIn's
        persistent "Unattended firmware update" preference.  If that preference
        is not enabled, the AddIn returns an ``approval_required`` error
        (surfaced as a ToolError).  There is no per-request approval token.

        Args:
            device_ref: The ref of the device to update.
            firmware: Firmware identifier / version string accepted by the AddIn.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> UpdateFirmware(
        [Description("The ref of the device to update.")] string device_ref,
        [Description("Firmware identifier / version string accepted by the AddIn.")] string firmware,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("firmware", firmware);
        var result = await bridge.CallAsync("firmware.update", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_job_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Poll the status of a running programming job.

        Args:
            job_id: The job ID returned by knx_program_device.
        """)]
    public async Task<JsonNode?> JobStatus(
        [Description("The job ID returned by knx_program_device.")] string job_id,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("jobId", job_id);
        var result = await bridge.CallAsync("job.status", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_job_cancel", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Cancel a running programming job.

        Args:
            job_id: The job ID returned by knx_program_device.
        """)]
    public async Task<JsonNode?> JobCancel(
        [Description("The job ID returned by knx_program_device.")] string job_id,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("jobId", job_id);
        var result = await bridge.CallAsync("job.cancel", parameters, null, cancellationToken);
        return result;
    }
}
