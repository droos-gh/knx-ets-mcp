using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Phase C: project management tools (port of tests/test_phase_c.py).</summary>
public sealed class PhaseCTests
{
    // -- knx_project_save (NOT SUPPORTED) -------------------------------------

    [Fact]
    public async Task ProjectSaveNotSupported()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_project_save");
        Assert.Contains("not_supported", ToolAssert.ErrorText(result));
    }

    // -- knx_project_export ---------------------------------------------------

    [Fact]
    public async Task ProjectExport()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_project_export", Args.Of(
            ("path", "C:\\Export\\project.knxproj"))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task ProjectExportWithCatalog()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_project_export", Args.Of(
            ("path", "C:\\Export\\full.knxproj"),
            ("include_catalog", true))));
        Assert.True(data.BoolAt("ok"));
    }

    // -- knx_project_backup (NOT SUPPORTED) -----------------------------------

    [Fact]
    public async Task ProjectBackupNotSupported()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_project_backup", Args.Of(
            ("path", "C:\\Backup\\project.bak")));
        Assert.Contains("not_supported", ToolAssert.ErrorText(result));
    }

    // -- knx_project_undo -----------------------------------------------------

    [Fact]
    public async Task ProjectUndo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_project_undo"));
        Assert.True(data.BoolAt("ok"));
    }

    // -- knx_project_redo -----------------------------------------------------

    [Fact]
    public async Task ProjectRedo()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_project_redo"));
        Assert.True(data.BoolAt("ok"));
    }

    // -- knx_project_export_semantic (NOT SUPPORTED) --------------------------

    [Fact]
    public async Task ProjectExportSemanticNotSupported()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_project_export_semantic");
        Assert.Contains("not_supported", ToolAssert.ErrorText(result));
    }
}
