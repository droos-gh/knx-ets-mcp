// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase D: Bus / Online operation tools.</summary>
[McpServerToolType]
public sealed class BusOnlineOperationTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_bus_ping", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Ping a KNX individual address to check if a device is alive.

        Direct return (not a job). Requires a bus interface selected in ETS.

        Args:
            address: Individual address in "area.line.device" format (e.g. "1.1.5").
        """)]
    public async Task<JsonNode?> BusPing(
        [Description("Individual address in \"area.line.device\" format (e.g. \"1.1.5\").")] string address,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("address", address);
        var result = await bridge.CallAsync("bus.ping", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_bus_scan_line", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Scan a topology line for responding KNX devices.

        Long-running: returns {jobId}. Poll with knx_job_status.
        The job result contains {addresses: ["1.1.0", "1.1.5", ...]}.

        Args:
            line_ref: The topology line ref (from knx_list_topology).
        """)]
    public async Task<JsonNode?> BusScanLine(
        [Description("The topology line ref (from knx_list_topology).")] string line_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref);
        var result = await bridge.CallAsync("bus.scanLine", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_reconstruct_line", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Reconstruct a topology line: scan the bus and compare/reconcile with the project.

        Long-running: returns {jobId}. Poll with knx_job_status.
        The job result contains {devices: [{address, maskVersion, serialNumber,
        manufacturerId, error}], scannedRange: "..."}.

        Args:
            line_ref: The topology line ref (from knx_topology).
        """)]
    public async Task<JsonNode?> ReconstructLine(
        [Description("The topology line ref (from knx_topology).")] string line_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref);
        var result = await bridge.CallAsync("bus.reconstructLine", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_read_group_objects", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Read group-object associations from a physical device; sync by default, use_job=true returns a jobId to poll.
        """)]
    public async Task<JsonNode?> ReadGroupObjects(
        string address,
        bool use_job = false,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("address", address)
            .SetFlag("async", use_job);
        var result = await bridge.CallAsync("device.readGroupObjects", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_device_read_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Read the device descriptor from a physical device on the KNX bus.

        Direct return. Returns {maskVersion, maskVersionId}.
        If the device is KNX Secure and no key is available, maskVersionId = "(secured)".

        Args:
            device_ref: The ref of the device (from knx_list_devices).
        """)]
    public async Task<JsonNode?> DeviceReadInfo(
        [Description("The ref of the device (from knx_list_devices).")] string device_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("device.readInfo", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_device_compare", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Compare a subset of device properties between the project and the bus.

        Returns {compared, partial, note}.  ``partial`` is always true because a
        full byte-level compare is not feasible via the SDK high-level API.
        Each entry in ``compared`` has {property, equal, expected, actual}.

        Args:
            device_ref: The ref of the device (from knx_list_devices).
        """)]
    public async Task<JsonNode?> DeviceCompare(
        [Description("The ref of the device (from knx_list_devices).")] string device_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("device.compare", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_group_read", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Read a group address value from the KNX bus.

        Direct return. Returns {value: "hex_string"} or {value: null} on timeout.
        Timeout is ~2.3 seconds (KNX standard).

        Args:
            ga_ref: The group address ref (from knx_list_group_addresses).
        """)]
    public async Task<JsonNode?> GroupRead(
        [Description("The group address ref (from knx_list_group_addresses).")] string ga_ref,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("gaRef", ga_ref);
        var result = await bridge.CallAsync("group.read", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_group_write", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Write a group value to the KNX bus.

        MUTATING: requires idempotencyKey. Returns {acknowledged: bool}.

        Args:
            ga_ref: The group address ref.
            value: Hex-encoded value bytes (e.g. "01" for on, "00" for off).
            less_7_bits: True if the value is <7 bits (fits in APCI byte).
            expected_project_revision: Optional revision guard.
        """)]
    public async Task<JsonNode?> GroupWrite(
        [Description("The group address ref.")] string ga_ref,
        [Description("Hex-encoded value bytes (e.g. \"01\" for on, \"00\" for off).")] string value,
        [Description("True if the value is <7 bits (fits in APCI byte).")] bool less_7_bits = false,
        [Description("Optional revision guard.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("gaRef", ga_ref)
            .Set("value", value)
            .SetFlag("less7Bits", less_7_bits);
        var result = await bridge.CallAsync("group.write", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_group_monitor", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Monitor group telegrams on the KNX bus for a bounded duration.

        Long-running: returns {jobId}. Poll with knx_job_status.
        The job result contains {telegrams: [{service, sourceAddress, groupAddress, value, timestamp}]}.

        NOTE (ETS 6.3.0): GroupMessageReceived does NOT fire for KNX Secure telegrams.

        Args:
            line_ref: Topology line ref, or "default" for default connection.
            duration_ms: Monitor duration in ms (1000-300000, default 10000).
        """)]
    public async Task<JsonNode?> GroupMonitor(
        [Description("Topology line ref, or \"default\" for default connection.")] string line_ref = "default",
        [Description("Monitor duration in ms (1000-300000, default 10000).")] int duration_ms = 10000,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref)
            .Set("durationMs", duration_ms);
        var result = await bridge.CallAsync("group.monitor", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_device_unload", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Unload a device (remove application program or full unload).

        Long-running: returns {jobId}. Poll with knx_job_status.
        MUTATING: requires idempotencyKey.

        Args:
            device_ref: The ref of the device to unload.
            full_unload: False = remove application only (default).
                True = full unload (application + individual address).
            expected_project_revision: Optional revision guard.
        """)]
    public async Task<JsonNode?> DeviceUnload(
        [Description("The ref of the device to unload.")] string device_ref,
        [Description("False = remove application only (default). True = full unload (application + individual address).")] bool full_unload = false,
        [Description("Optional revision guard.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .SetFlag("fullUnload", full_unload);
        var result = await bridge.CallAsync("device.unload", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_bus_set_individual_address", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("""
        Write a new individual address to a physical device on the KNX bus (job-based).
        """)]
    public async Task<JsonNode?> BusSetIndividualAddress(
        string device_ref,
        string current_address,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("currentAddress", current_address);
        var result = await bridge.CallAsync("bus.setIndividualAddress", parameters, null, cancellationToken);
        return result;
    }
}
