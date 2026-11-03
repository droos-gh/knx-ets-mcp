using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    // Methods allowed inside batch.apply -- mirrors the C# BatchableMethods allowlist
    // (Python _BATCHABLE_METHODS). Fast, undo-reversible project mutations only; no
    // bus/programming/catalog-import ops, no nested batch.
    private static readonly HashSet<string> BatchableMethods = new(StringComparer.Ordinal)
    {
        "ga.create", "ga.delete", "ga.rename", "ga.setDescription", "ga.setDatapointType",
        "groupRange.create", "groupRange.delete",
        "device.addFromCatalog", "device.delete", "device.rename",
        "device.setAddress", "device.unassign",
        "device.setDescription", "device.setComment",
        "link.create", "link.delete",
        "param.set", "param.setDefault",
        "area.create", "area.delete",
        "line.create", "line.delete",
        "comObject.setFlags", "comObject.setDescription", "comObject.setFunctionText",
        "building.create", "building.delete", "building.rename",
        "building.assignDevice", "building.unassignDevice",
        "buildingFunction.create", "buildingFunction.delete",
        "buildingFunction.linkGroupAddress", "buildingFunction.unlinkGroupAddress",
        "groupRange.moveGroupAddress", "groupRange.moveGroupRange",
        "line.move", "buildingPart.move",
        "device.addAdditionalAddress", "device.removeAdditionalAddress",
        "line.addAdditionalGroupAddress", "line.removeAdditionalGroupAddress",
        "segment.create", "segment.delete",
        "trade.create", "trade.delete",
        "trade.assignDevice", "trade.unassignDevice",
        "busInterface.link", "busInterface.unlink",
        "certificate.add", "certificate.delete",
        "tag.create", "tag.delete",
        "todo.create", "todo.delete",
        "projectHistory.add", "projectHistory.delete",
    };

    // Required params per batchable method (mirrors the C# BatchRequiredParams, Python
    // _BATCH_REQUIRED_PARAMS) for the validateOnly structural check.
    private static readonly Dictionary<string, string[]> BatchRequiredParams = new(StringComparer.Ordinal)
    {
        ["link.create"] = ["comObjectRef", "gaRef"],
        ["link.delete"] = ["comObjectRef", "gaRef"],
        ["ga.create"] = ["name", "address"],
        ["ga.delete"] = ["gaRef"],
        ["ga.rename"] = ["gaRef", "name"],
        ["ga.setDescription"] = ["gaRef"],
        ["ga.setDatapointType"] = ["gaRef"],
        ["param.set"] = ["deviceRef", "parameterRef", "value"],
        ["param.setDefault"] = ["deviceRef", "parameterRef"],
        ["comObject.setFlags"] = ["comObjectRef"],
        ["comObject.setDescription"] = ["comObjectRef"],
        ["comObject.setFunctionText"] = ["comObjectRef"],
        ["device.rename"] = ["deviceRef", "name"],
        ["device.setAddress"] = ["deviceRef", "address"],
    };

    private JsonNode? HandleBatchApply(JsonNode? p)
    {
        if (Get(p, "operations") is not JsonArray operations || operations.Count == 0)
            throw Fail("invalid_params", "batch.apply requires a non-empty 'operations' array");
        if (operations.Count > MaxBatchOperations)
        {
            throw Fail("invalid_params",
                $"batch.apply 'operations' exceeds the maximum of {MaxBatchOperations}");
        }
        // Python: atomic = params.get("atomic", True); a non-bool counts as True.
        var atomicNode = GetOr(p, "atomic", true);
        var atomic = Py.Kind(atomicNode) != System.Text.Json.JsonValueKind.False;

        // Validate all steps up front (fail fast, nothing executed yet).
        var parsed = new List<(string Method, JsonNode? Params)>();
        foreach (var op in operations)
        {
            if (op is not JsonObject opObject || Py.Kind(opObject["method"]) != System.Text.Json.JsonValueKind.String)
            {
                throw Fail("invalid_params",
                    "each batch operation must be an object with a 'method' string");
            }
            var subMethod = S(opObject["method"]);
            if (!BatchableMethods.Contains(subMethod))
                throw Fail("invalid_params", $"Method '{subMethod}' is not allowed inside batch.apply");
            // Python: op.get("params", {}) or {} -- a falsy params value becomes {}.
            var subParams = GetOr(opObject, "params", null);
            parsed.Add((subMethod, Py.Truthy(subParams) ? subParams : new JsonObject()));
        }

        // validateOnly: read-only pre-flight -- report per-op issues without mutating.
        if (Py.Kind(Get(p, "validateOnly")) == System.Text.Json.JsonValueKind.True)
        {
            var validationResults = new JsonArray();
            int validCount = 0, invalidCount = 0;
            for (var i = 0; i < parsed.Count; i++)
            {
                var (subMethod, subParams) = parsed[i];
                var issues = ValidateBatchOp(subMethod, subParams);
                var ok = issues.Count == 0;
                Push(validationResults, new JsonObject
                {
                    ["index"] = i,
                    ["method"] = subMethod,
                    ["valid"] = ok,
                    ["issues"] = issues,
                });
                if (ok)
                    validCount++;
                else
                    invalidCount++;
            }
            return new JsonObject
            {
                ["validated"] = true,
                ["atomic"] = atomic,
                ["total"] = parsed.Count,
                ["valid"] = validCount,
                ["invalid"] = invalidCount,
                ["results"] = validationResults,
            };
        }

        var snapshot = atomic ? _state.DeepCopy() : null;
        var results = new JsonArray();
        int okCount = 0, failedCount = 0, skippedCount = 0;
        var failed = false;

        for (var i = 0; i < parsed.Count; i++)
        {
            var (subMethod, subParams) = parsed[i];
            if (atomic && failed)
            {
                Push(results, new JsonObject { ["index"] = i, ["method"] = subMethod, ["status"] = "skipped" });
                skippedCount++;
                continue;
            }
            try
            {
                var subResult = _handlers[subMethod](subParams);
                Push(results, new JsonObject
                {
                    ["index"] = i,
                    ["method"] = subMethod,
                    ["status"] = "ok",
                    ["result"] = subResult,
                });
                okCount++;
            }
            catch (MockProtocolException ex)
            {
                // Only protocol errors are per-step results; any other exception (a Python
                // crash) propagates out of send() without a rollback, as in Python.
                Push(results, new JsonObject
                {
                    ["index"] = i,
                    ["method"] = subMethod,
                    ["status"] = "error",
                    ["error"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message },
                });
                failedCount++;
                failed = true;
            }
        }

        if (atomic && failed && snapshot is not null)
            _state = snapshot; // roll back all applied steps (Python _restore_state)

        // The mock restores from a full snapshot, so its rollback is always clean; the AddIn
        // reports rollbackNote when the SDK discard did not complete cleanly (see IpcServer).
        return new JsonObject
        {
            ["applied"] = atomic ? !failed : true,
            ["atomic"] = atomic,
            ["rolledBack"] = atomic && failed,
            ["rollbackNote"] = null,
            ["total"] = parsed.Count,
            ["ok"] = okCount,
            ["failed"] = failedCount,
            ["skipped"] = skippedCount,
            ["results"] = results,
        };
    }

    /// <summary>Read-only pre-flight for one batch op (mirrors the C# ValidateOneOp).</summary>
    private JsonArray ValidateBatchOp(string method, JsonNode? p)
    {
        var issues = new JsonArray();
        if (BatchRequiredParams.TryGetValue(method, out var required))
        {
            foreach (var key in required) // structural
            {
                var v = Get(p, key);
                if (Py.IsNone(v) || Py.Eq(v, ""))
                    Push(issues, $"missing required parameter '{key}'");
            }
        }

        if (method == "link.create")
        {
            var coRef = Get(p, "comObjectRef");
            var gaRef = Get(p, "gaRef");
            if (Py.Truthy(coRef) && Py.Truthy(gaRef))
            {
                var co = Find(_state.ComObjects, coRef);
                if (co is null)
                    Push(issues, $"comObject {Py.Str(coRef)} not found");
                else if (!Py.Truthy(GetOr(co, "isActive", true)))
                    Push(issues, $"comObject {Py.Str(coRef)} is inactive and cannot be linked");
                var ga = Find(_state.GroupAddresses, gaRef);
                if (ga is null)
                    Push(issues, $"groupAddress {Py.Str(gaRef)} not found");
                if (co is not null && ga is not null)
                {
                    var warning = DptMismatchWarning(
                        Py.Truthy(co["dpt"]) ? co["dpt"] : null,
                        Py.Truthy(ga["dpt"]) ? ga["dpt"] : null);
                    if (!string.IsNullOrEmpty(warning))
                        Push(issues, warning);
                }
            }
        }
        else if (method == "ga.create")
        {
            var address = Get(p, "address");
            if (Py.Truthy(address)
                && _state.GroupAddresses.Values.Any(g => Py.Eq(GetOr(g, "address", null), address)))
            {
                Push(issues, $"group address {Py.Str(address)} already exists");
            }
        }
        return issues;
    }
}
