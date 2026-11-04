// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol;
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

        operations: a non-empty JSON array of objects, each {"method": <name>, "params": {...}},
          in execution order. Pass a real JSON array, not a string -- e.g.
          [{"method": "ga.create", "params": {"name": "A", "address": "1/0/1"}}].
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
        // The `operations` parameter has no concrete .NET type, so the generated tool schema
        // is untyped; some MCP clients then send the array serialised as a JSON *string*
        // rather than as a JSON array. Accept both forms here (a real array, or a
        // string-encoded array) and reject anything else with a clear, typed error -- so the
        // bridge always receives a genuine array instead of refusing a stringified one.
        var opsArray = NormalizeOperations(operations);
        var parameters = new JsonObject();
        parameters["operations"] = opsArray; // freshly parsed, so never already parented
        parameters
            .Set("atomic", atomic)
            .Set("validateOnly", validate_only);
        var result = await bridge.CallAsync("batch.apply", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    /// <summary>
    /// Coerces the <c>operations</c> argument into a non-empty JSON array. Accepts a JSON
    /// array as-is, and a JSON string that itself contains an array (the shape some clients
    /// send for an untyped parameter). Any other shape -- or an empty array -- throws an
    /// <c>invalid_params</c> tool error that names the JSON type actually received.
    /// </summary>
    internal static JsonArray NormalizeOperations(JsonElement operations)
    {
        JsonNode? node;
        if (operations.ValueKind == JsonValueKind.String)
        {
            var raw = operations.GetString() ?? string.Empty;
            try
            {
                node = JsonNode.Parse(raw);
            }
            catch (JsonException ex)
            {
                throw Invalid(
                    "'operations' was sent as a JSON string that is not valid JSON "
                  + $"({ex.Message}). Send a JSON array of {{\"method\",\"params\"}} objects.");
            }

            if (node is not JsonArray)
            {
                throw Invalid(
                    $"'operations' was sent as a JSON string, but its content is a {Describe(node)}, "
                  + "not an array. Send a JSON array of {\"method\",\"params\"} objects.");
            }
        }
        else if (operations.ValueKind == JsonValueKind.Array)
        {
            node = JsonNode.Parse(operations.GetRawText());
        }
        else
        {
            throw Invalid(
                $"'operations' must be a JSON array of {{\"method\",\"params\"}} objects, "
              + $"but a JSON {Describe(operations.ValueKind)} was received.");
        }

        if (node is not JsonArray array)
            throw Invalid("'operations' must be a JSON array of {\"method\",\"params\"} objects.");
        if (array.Count == 0)
            throw Invalid("'operations' must be a non-empty array of {\"method\",\"params\"} objects.");
        return array;
    }

    // The protocol error code the LLM sees in "[code] message" (docs/protocol.md); the mock
    // and the AddIn use the same literal for a bad-argument rejection.
    private static McpException Invalid(string message)
        => new($"[invalid_params] {message}");

    /// <summary>A human-readable name for the JSON type of a parsed node.</summary>
    private static string Describe(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "array",
        JsonObject => "object",
        JsonValue value => Describe(value.GetValueKind()),
        _ => "value",
    };

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        _ => "value",
    };
}
