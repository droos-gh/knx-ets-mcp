// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase C: Project management tools.</summary>
[McpServerToolType]
public sealed class ProjectManagementTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_project_save", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Save the KNX project. NOT SUPPORTED: ETS auto-saves projects.
        """)]
    public async Task<JsonNode?> ProjectSave(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("project.save", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_project_export", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Export the KNX project to a .knxproj file.

        Args:
            path: Absolute path for the export file on the ETS host.
            include_catalog: Include catalog data in the export.
        """)]
    public async Task<JsonNode?> ProjectExport(
        [Description("Absolute path for the export file on the ETS host.")] string path,
        [Description("Include catalog data in the export.")] bool include_catalog = false,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("path", path)
            .Set("includeCatalog", include_catalog);
        var result = await bridge.CallAsync("project.export", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_project_backup", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Backup the KNX project database. NOT SUPPORTED in ETS 6.

        Root.BackupDatabase is a backwards compatibility stub with no effect.

        Args:
            path: Absolute path for the backup file.
        """)]
    public async Task<JsonNode?> ProjectBackup(
        [Description("Absolute path for the backup file.")] string path,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("path", path);
        var result = await bridge.CallAsync("project.backup", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_project_undo", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Undo the last operation in the KNX project.

        Args:
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> ProjectUndo(
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("project.undo", null, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_project_redo", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Redo a previously undone operation in the KNX project.

        Args:
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> ProjectRedo(
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("project.redo", null, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_project_export_semantic", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Export semantic data (Turtle/JSON-LD). NOT SUPPORTED in v1.

        Root.ExportSemanticDataAsync is async and would deadlock on the UI thread.
        """)]
    public async Task<JsonNode?> ProjectExportSemantic(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("project.exportSemantic", null, null, cancellationToken);
        return result;
    }
}
