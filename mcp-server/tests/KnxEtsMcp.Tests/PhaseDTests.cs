using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Phase D: bus / online operation tools (port of tests/test_phase_d.py).</summary>
public sealed class PhaseDTests
{
    // -- knx_bus_ping ---------------------------------------------------------

    [Fact]
    public async Task BusPingAlive()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_ping", Args.Of(("address", "1.1.1"))));
        Assert.True(data.BoolAt("alive"));
    }

    [Fact]
    public async Task BusPingNotAlive()
    {
        // Mock: addresses ending in .0 are "not alive"
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_ping", Args.Of(("address", "1.1.0"))));
        Assert.False(data.BoolAt("alive"));
    }

    [Fact]
    public async Task BusPingInvalidAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_bus_ping", Args.Of(("address", "bad")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- knx_bus_scan_line ----------------------------------------------------

    [Fact]
    public async Task BusScanLine()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_scan_line", Args.Of(("line_ref", "line-1.1"))));
        Assert.True(data.ContainsKey("jobId"));
        var jobId = data.StringAt("jobId");

        // Poll job status
        var status = ToolAssert.Object(await host.CallAsync("knx_job_status", Args.Of(("job_id", jobId))));
        Assert.Equal("done", status.StringAt("state"));
        Assert.True(status.ContainsKey("result"));
        var addresses = status.ObjectAt("result").ArrayAt("addresses").Strings();
        Assert.Contains("1.1.1", addresses);
        Assert.Contains("1.1.2", addresses);
    }

    [Fact]
    public async Task BusScanLineNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_bus_scan_line", Args.Of(("line_ref", "line-9.9")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_device_read_info -------------------------------------------------

    [Fact]
    public async Task DeviceReadInfo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_read_info", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.ContainsKey("maskVersion"));
        Assert.True(data.ContainsKey("maskVersionId"));
        Assert.Equal("0x0705", data.StringAt("maskVersion"));
    }

    [Fact]
    public async Task DeviceReadInfoNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_device_read_info", Args.Of(("device_ref", "dev-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_device_compare ---------------------------------------------------

    [Fact]
    public async Task DeviceCompare()
    {
        // device.compare returns a partial comparison result.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_compare", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.BoolAt("partial"));
        Assert.NotEmpty(data.ArrayAt("compared"));
    }

    // -- knx_group_read -------------------------------------------------------

    [Fact]
    public async Task GroupReadBoolean()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_group_read", Args.Of(("ga_ref", "ga-0001"))));
        Assert.Equal("01", data.StringAt("value"));  // DPT 1.001 -> boolean value
    }

    [Fact]
    public async Task GroupReadPercentage()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_group_read", Args.Of(("ga_ref", "ga-0002"))));
        Assert.Equal("64", data.StringAt("value"));  // DPT 5.001 -> percentage value
    }

    [Fact]
    public async Task GroupReadNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_group_read", Args.Of(("ga_ref", "ga-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_group_write ------------------------------------------------------

    [Fact]
    public async Task GroupWrite()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_group_write", Args.Of(
            ("ga_ref", "ga-0001"),
            ("value", "01"))));
        Assert.True(data.BoolAt("acknowledged"));
    }

    [Fact]
    public async Task GroupWriteLess7Bits()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_group_write", Args.Of(
            ("ga_ref", "ga-0001"),
            ("value", "01"),
            ("less_7_bits", true))));
        Assert.True(data.BoolAt("acknowledged"));
    }

    [Fact]
    public async Task GroupWriteInvalidHex()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_group_write", Args.Of(
            ("ga_ref", "ga-0001"),
            ("value", "ZZ")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task GroupWriteNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_group_write", Args.Of(
            ("ga_ref", "ga-9999"),
            ("value", "01")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_group_monitor ----------------------------------------------------

    [Fact]
    public async Task GroupMonitor()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_group_monitor", Args.Of(
            ("line_ref", "default"),
            ("duration_ms", 5000))));
        Assert.True(data.ContainsKey("jobId"));
        var jobId = data.StringAt("jobId");

        // Poll job status
        var status = ToolAssert.Object(await host.CallAsync("knx_job_status", Args.Of(("job_id", jobId))));
        Assert.Equal("done", status.StringAt("state"));
        var telegrams = status.ObjectAt("result").ArrayAt("telegrams");
        Assert.Equal(2, telegrams.Count);
        var t0 = telegrams[0].AsObj();
        Assert.True(t0.ContainsKey("service"));
        Assert.True(t0.ContainsKey("sourceAddress"));
        Assert.True(t0.ContainsKey("groupAddress"));
        Assert.True(t0.ContainsKey("value"));
        Assert.True(t0.ContainsKey("timestamp"));
    }

    [Fact]
    public async Task GroupMonitorLineNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_group_monitor", Args.Of(
            ("line_ref", "line-9.9"),
            ("duration_ms", 5000)));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task GroupMonitorInvalidDuration()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_group_monitor", Args.Of(
            ("duration_ms", 100)));  // Too short
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- knx_device_unload ----------------------------------------------------

    [Fact]
    public async Task DeviceUnload()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_unload", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task DeviceUnloadFull()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_device_unload", Args.Of(
            ("device_ref", "dev-0001"),
            ("full_unload", true))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task DeviceUnloadNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_device_unload", Args.Of(("device_ref", "dev-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_program_device (extended with options) ---------------------------

    [Fact]
    public async Task ProgramDeviceDefault()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_program_device", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task ProgramDevicePartial()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_program_device", Args.Of(
            ("device_ref", "dev-0001"),
            ("options", "partial"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task ProgramDeviceApplication()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_program_device", Args.Of(
            ("device_ref", "dev-0001"),
            ("options", "application"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task ProgramDeviceInvalidOption()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_program_device", Args.Of(
            ("device_ref", "dev-0001"),
            ("options", "bogus")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- knx_bus_set_individual_address (v0.1.13: job-based) ------------------

    [Fact]
    public async Task BusSetIndividualAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_bus_set_individual_address", Args.Of(
            ("device_ref", "dev-0001"),
            ("current_address", "1.1.1"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task BusSetIndividualAddressNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_bus_set_individual_address", Args.Of(
            ("device_ref", "dev-9999"),
            ("current_address", "1.1.1")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }
}
