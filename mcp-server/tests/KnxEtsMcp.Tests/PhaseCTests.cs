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
    public async Task ProjectUndoRevertsTheLastChange()
    {
        await using var host = await McpTestHost.StartAsync();
        await host.CallAsync("knx_create_group_address", Args.Of(("name", "Essai"), ("address", "1/0/9")));
        Assert.Contains("Essai", await GaNames(host));

        var data = ToolAssert.Object(await host.CallAsync("knx_project_undo"));
        Assert.True(data.BoolAt("ok"));
        Assert.DoesNotContain("Essai", await GaNames(host));
    }

    [Fact]
    public async Task ProjectUndoWithNothingToUndoErrors()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_project_undo");
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- knx_project_redo -----------------------------------------------------

    [Fact]
    public async Task ProjectRedoReappliesAnUndoneChange()
    {
        await using var host = await McpTestHost.StartAsync();
        await host.CallAsync("knx_create_group_address", Args.Of(("name", "Essai"), ("address", "1/0/9")));
        await host.CallAsync("knx_project_undo");
        Assert.DoesNotContain("Essai", await GaNames(host));

        var data = ToolAssert.Object(await host.CallAsync("knx_project_redo"));
        Assert.True(data.BoolAt("ok"));
        Assert.Contains("Essai", await GaNames(host));
    }

    private static async Task<List<string>> GaNames(McpTestHost host)
    {
        var list = ToolAssert.Array(await host.CallAsync("knx_list_group_addresses"));
        return list.Select(ga => ga.ObjB()["name"].StrB()).ToList();
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
