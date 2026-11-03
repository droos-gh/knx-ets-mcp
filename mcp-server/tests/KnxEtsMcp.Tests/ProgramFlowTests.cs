using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// Programming / firmware workflow (port of tests/test_program_flow.py).
/// device.program returns a jobId; job.status and job.cancel are functional.
/// firmware.update is gated by the AddIn's persistent "Unattended firmware update"
/// preference; the mock simulates the default (off) and always returns
/// approval_required. There is no per-request approval token.
/// </summary>
public sealed class ProgramFlowTests
{
    // -- device.program (automatic, no approval token) ------------------------

    [Fact]
    public async Task ProgramDevice()
    {
        // device.program returns a jobId for the download operation.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_program_device", Args.Of(("device_ref", "dev-0001"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task ProgramDeviceWithOptions()
    {
        // device.program accepts options as a string.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_program_device", Args.Of(
            ("device_ref", "dev-0001"),
            ("options", "partial"))));
        Assert.True(data.ContainsKey("jobId"));
    }

    [Fact]
    public async Task ProgramDeviceNotFound()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_program_device", Args.Of(("device_ref", "dev-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- firmware.update (preference-gated, no per-request token) -------------

    [Fact]
    public async Task FirmwareUpdateApprovalRequired()
    {
        // firmware.update returns approval_required when the AddIn preference is off.
        // The mock simulates the default state ("Unattended firmware update" = off).
        // No approval_token parameter exists; the gate is solely the AddIn preference.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_update_firmware", Args.Of(
            ("device_ref", "dev-0001"),
            ("firmware", "fw-v2.0")));
        Assert.Contains("approval_required", ToolAssert.ErrorText(result));
    }

    // -- job.status / job.cancel (functional) ---------------------------------

    [Fact]
    public async Task JobStatusNotFound()
    {
        // job.status returns not_found for unknown job IDs.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_job_status", Args.Of(("job_id", "any-job-id")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task JobCancelNotFound()
    {
        // job.cancel returns not_found for unknown job IDs.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_job_cancel", Args.Of(("job_id", "any-job-id")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task JobLifecycle()
    {
        // Full lifecycle: program -> status -> cancel.
        await using var host = await McpTestHost.StartAsync();

        // Start a program job
        var data = ToolAssert.Object(await host.CallAsync("knx_program_device", Args.Of(("device_ref", "dev-0001"))));
        var jobId = data.StringAt("jobId");

        // Check status
        var status = ToolAssert.Object(await host.CallAsync("knx_job_status", Args.Of(("job_id", jobId))));
        Assert.Equal("done", status.StringAt("state"));

        // Cancel (idempotent on terminal state)
        var cancel = ToolAssert.Object(await host.CallAsync("knx_job_cancel", Args.Of(("job_id", jobId))));
        Assert.True(cancel.BoolAt("ok"));
    }
}
