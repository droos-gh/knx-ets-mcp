// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Batch operations.</summary>
[McpServerToolType]
public sealed class BatchTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_batch_apply", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Run many project mutations in one call under a single undo marker.

        Use this to parametrise and link across many devices/channels in one pass
        instead of dozens of single calls (fewer round-trips, one undo step).

        operations: list of {"method": <name>, "params": {...}} in execution order.
          Only fast, undo-reversible project mutations are allowed (ga.*, link.*,
          param.*, device.* label/address, comObject.*, building*, groupRange.*,
          area/line/segment/trade/tag/todo, etc.). NOT allowed: device.program,
          firmware.update, bus.*, group.write, device.reset, catalog.import/
          internalize, project.undo/redo, or a nested batch.apply.
        atomic (default true): all-or-nothing. The first failing step rolls back the
          whole batch and the rest are reported as "skipped" (applied=false). Set
          false for best-effort: each step is independent, failures do not stop
          later steps (applied=true, partial).
        validate_only (default false): read-only PRE-FLIGHT. Nothing is mutated; instead
          each op is checked and the result is {validated, atomic, total, valid, invalid,
          results:[{index, method, valid, issues[]}]}. Checks: required params present,
          link.create -> com-object exists+active, GA exists, DPT match; ga.create -> address
          not already in use. RECOMMENDED before a large batch: validate first, fix the ops
          that report issues, then apply with validate_only=false.
          NOTE: validation is against the CURRENT project state, not the post-batch state -- an
          op that depends on a prerequisite created by an EARLIER op in the same batch (e.g.
          link to a GA that op #1 creates) may be reported invalid yet still succeed on apply.

        On apply, returns {applied, atomic, rolledBack, rollbackNote?, total, ok, failed,
        skipped, results[]}, where each results[i] is {index, method, status: ok|error|skipped,
        result?, error?}. A step is never reported ok unless it actually ran successfully.
        `rolledBack` is true only when the failed atomic batch was rolled back cleanly; if the
        SDK could not fully undo it, `rolledBack` is false and `rollbackNote` says the project
        may hold partial changes (re-read to confirm; prefer atomic=false on modular devices).
        """)]
    public async Task<JsonNode?> BatchApply(
        JsonElement operations,
        bool atomic = true,
        bool validate_only = false,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("operations", operations)
            .Set("atomic", atomic)
            .Set("validateOnly", validate_only);
        var result = await bridge.CallAsync("batch.apply", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
