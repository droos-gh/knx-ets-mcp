using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    // Canonical todo status values (matches C# ToDoItemInfo), Python _VALID_TODO_STATUSES.
    private static readonly HashSet<string> ValidTodoStatuses = new(StringComparer.Ordinal) { "Open", "Accomplished" };

    // -- Phase C: Project management handlers ---------------------------------------------

    private JsonNode? HandleProjectSave(JsonNode? _)
        => throw Fail("not_supported", "project.save is not supported: ETS auto-saves projects.");

    private JsonNode? HandleProjectExport(JsonNode? p)
    {
        if (!Py.Truthy(Get(p, "path")))
            throw Fail("invalid_params", "path is required");
        // Mock: always succeed
        return OkTrue();
    }

    private JsonNode? HandleProjectBackup(JsonNode? _)
        => throw Fail("not_supported",
            "project.backup is not supported: Root.BackupDatabase has no effect in ETS 6.");

    private JsonNode? HandleProjectUndo(JsonNode? _)
    {
        // Mirror the AddIn (UndoManager): undoing with an empty stack is an explicit error,
        // not a silent ok, and it must not advance the revision.
        if (_undoStack.Count == 0)
            throw Fail("invalid_params", "Nothing to undo: no undoable action on the stack.");

        var redoEntry = SnapshotNow();                 // current state, so redo can restore it
        var entry = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        RestoreSnapshot(entry);                         // revert to the pre-mutation state
        _redoStack.Add(redoEntry);
        return OkTrue();
    }

    private JsonNode? HandleProjectRedo(JsonNode? _)
    {
        if (_redoStack.Count == 0)
            throw Fail("invalid_params", "Nothing to redo: no undone action to restore.");

        var undoEntry = SnapshotNow();                 // current state, so a later undo can restore it
        var entry = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        RestoreSnapshot(entry);                         // re-apply the undone state
        _undoStack.Add(undoEntry);
        return OkTrue();
    }

    private JsonNode? HandleProjectExportSemantic(JsonNode? _)
        => throw Fail("not_supported", "project.exportSemantic is not supported in v1.");

    // -- Phase G: Navigation ----------------------------------------------------------------

    private JsonNode? HandleNavigateTo(JsonNode? p)
    {
        if (!Py.Truthy(Get(p, "ref")))
            throw Fail("invalid_params", "ref is required");
        // Accept any ref -- in real ETS, this navigates the UI
        return OkTrue();
    }

    // -- Phase G: Trades --------------------------------------------------------------------

    private JsonNode? HandleTradesList(JsonNode? _) => CopyList(_state.Trades.Values);

    private JsonNode? HandleTradeCreate(JsonNode? p)
    {
        var name = Get(p, "name");
        if (!Py.Truthy(name))
            throw Fail("invalid_params", "name is required");
        _state.TradeCounter++;
        var tradeRef = MakeRef("trade");
        var trade = new JsonObject
        {
            ["ref"] = tradeRef,
            ["name"] = Clone(name),
            ["number"] = _state.TradeCounter,
            ["devices"] = new JsonArray(),
        };
        _state.Trades[tradeRef] = trade;
        BumpRevision();
        return trade.DeepClone();
    }

    private JsonNode? HandleTradeDelete(JsonNode? p)
    {
        var tradeRef = Get(p, "tradeRef");
        if (!Py.Truthy(tradeRef))
            throw Fail("invalid_params", "tradeRef is required");
        if (!Has(_state.Trades, tradeRef))
            throw Fail("not_found", $"Trade {Py.Str(tradeRef)} not found");
        _state.Trades.Remove(S(tradeRef));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleTradeAssignDevice(JsonNode? p)
    {
        var tradeRef = Get(p, "tradeRef");
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(tradeRef) || !Py.Truthy(deviceRef))
            throw Fail("invalid_params", "tradeRef and deviceRef are required");
        var trade = Find(_state.Trades, tradeRef)
            ?? throw Fail("not_found", $"Trade {Py.Str(tradeRef)} not found");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        var devices = (JsonArray)trade["devices"]!;
        if (!Py.ListContains(devices, deviceRef))
            Push(devices, Clone(deviceRef));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleTradeUnassignDevice(JsonNode? p)
    {
        var tradeRef = Get(p, "tradeRef");
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(tradeRef) || !Py.Truthy(deviceRef))
            throw Fail("invalid_params", "tradeRef and deviceRef are required");
        var trade = Find(_state.Trades, tradeRef)
            ?? throw Fail("not_found", $"Trade {Py.Str(tradeRef)} not found");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        var devices = (JsonArray)trade["devices"]!;
        var index = Py.IndexOf(devices, deviceRef);
        if (index >= 0)
            devices.RemoveAt(index);
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: Parameter default ------------------------------------------------------------

    private JsonNode? HandleParamSetDefault(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var parameterRef = Get(p, "parameterRef");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(parameterRef))
            throw Fail("invalid_params", "deviceRef and parameterRef are required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        var param = Find(_state.Parameters, parameterRef)
            ?? throw Fail("not_found", $"Parameter {Py.Str(parameterRef)} not found");
        if (!Py.Eq(param["deviceRef"], deviceRef))
        {
            throw Fail("invalid_params",
                $"Parameter {Py.Str(parameterRef)} does not belong to device {Py.Str(deviceRef)}");
        }
        param["value"] = Clone(param["defaultValue"]);
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: Certificates ---------------------------------------------------------------

    private JsonNode? HandleCertificatesList(JsonNode? _) => CopyList(_state.Certificates.Values);

    private JsonNode? HandleCertificateAdd(JsonNode? p)
    {
        var value = Get(p, "value");
        if (!Py.Truthy(value))
            throw Fail("invalid_params", "value is required");
        var certRef = MakeRef("cert");
        _state.Certificates[certRef] = new JsonObject
        {
            ["ref"] = certRef,
            ["serialNumber"] = SlicePrefix16(value),
            ["deviceRef"] = null,
        };
        BumpRevision();
        return new JsonObject { ["ref"] = certRef, ["serialNumber"] = SlicePrefix16(value), ["deviceRef"] = null };
    }

    /// <summary>Python <c>value[:16]</c> for a str or list; other types are not sliceable.</summary>
    private static JsonNode? SlicePrefix16(JsonNode? value)
    {
        if (Py.AsStringOrNull(value) is { } text)
            return Py.Prefix(text, 16);
        if (value is JsonArray list)
        {
            var slice = new JsonArray();
            foreach (var item in list.Take(16))
                Push(slice, Clone(item));
            return slice;
        }
        throw Py.Crash($"TypeError: '{Py.TypeName(value)}' object is not subscriptable");
    }

    private JsonNode? HandleCertificateDelete(JsonNode? p)
    {
        var certRef = Get(p, "certificateRef");
        if (!Py.Truthy(certRef))
            throw Fail("invalid_params", "certificateRef is required");
        if (!Has(_state.Certificates, certRef))
            throw Fail("not_found", $"Certificate {Py.Str(certRef)} not found");
        _state.Certificates.Remove(S(certRef));
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: Tags ---------------------------------------------------------------------

    private JsonNode? HandleTagsList(JsonNode? _) => CopyList(_state.Tags.Values);

    private JsonNode? HandleTagCreate(JsonNode? p)
    {
        var label = Get(p, "label");
        if (!Py.Truthy(label))
            throw Fail("invalid_params", "label is required");
        var color = Py.RequireStr(GetOr(p, "color", "#808080"), "startswith");
        // Ensure color is #RRGGBB (production always returns leading #)
        if (!color.StartsWith('#'))
            color = "#" + color;
        _state.TagCounter++;
        var tagRef = $"tag:t{_state.TagCounter}";
        var tag = new JsonObject { ["ref"] = tagRef, ["label"] = Clone(label), ["color"] = color };
        _state.Tags[tagRef] = tag;
        BumpRevision();
        return tag.DeepClone();
    }

    private JsonNode? HandleTagDelete(JsonNode? p)
    {
        var tagRef = Get(p, "tagRef");
        if (!Py.Truthy(tagRef))
            throw Fail("invalid_params", "tagRef is required");
        if (!Has(_state.Tags, tagRef))
            throw Fail("not_found", $"Tag {Py.Str(tagRef)} not found");
        _state.Tags.Remove(S(tagRef));
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: ToDos --------------------------------------------------------------------

    private JsonNode? HandleTodosList(JsonNode? _) => CopyList(_state.Todos.Values);

    private JsonNode? HandleTodoCreate(JsonNode? p)
    {
        var description = Get(p, "description");
        if (!Py.Truthy(description))
            throw Fail("invalid_params", "description is required");
        // Canonical status: Open (default) or Accomplished
        var status = GetOr(p, "status", "Open");
        if (Py.HashKey(status) is not { } statusText || !ValidTodoStatuses.Contains(statusText))
        {
            // "Accomplished, Open" is Python's ', '.join(sorted(_VALID_TODO_STATUSES)).
            throw Fail("invalid_params", $"Invalid status: '{Py.Str(status)}'. Valid: Accomplished, Open");
        }
        _state.TodoCounter++;
        var todoRef = $"todo:t{_state.TodoCounter}";
        var todo = new JsonObject
        {
            ["ref"] = todoRef,
            ["description"] = Clone(description),
            ["objectPath"] = Clone(GetOr(p, "objectPath", "")),
            ["status"] = statusText,
        };
        _state.Todos[todoRef] = todo;
        BumpRevision();
        return todo.DeepClone();
    }

    private JsonNode? HandleTodoDelete(JsonNode? p)
    {
        var todoRef = Get(p, "todoRef");
        if (!Py.Truthy(todoRef))
            throw Fail("invalid_params", "todoRef is required");
        if (!Has(_state.Todos, todoRef))
            throw Fail("not_found", $"ToDo {Py.Str(todoRef)} not found");
        _state.Todos.Remove(S(todoRef));
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: Project history -------------------------------------------------------------

    private JsonNode? HandleProjectHistoryList(JsonNode? _) => CopyList(_state.ProjectHistory.Values);

    private JsonNode? HandleProjectHistoryAdd(JsonNode? p)
    {
        var text = Get(p, "text");
        if (!Py.Truthy(text))
            throw Fail("invalid_params", "text is required");
        _state.HistoryCounter++;
        var historyRef = $"ph:h{_state.HistoryCounter}";
        var entry = new JsonObject
        {
            ["ref"] = historyRef,
            ["date"] = "2025-06-01T12:00:00Z",
            ["text"] = Clone(text),
            ["detail"] = "",
            ["user"] = "mcp-bridge",
        };
        _state.ProjectHistory[historyRef] = entry;
        BumpRevision();
        return entry.DeepClone();
    }

    private JsonNode? HandleProjectHistoryDelete(JsonNode? p)
    {
        var historyRef = Get(p, "historyRef");
        if (!Py.Truthy(historyRef))
            throw Fail("invalid_params", "historyRef is required");
        if (!Has(_state.ProjectHistory, historyRef))
            throw Fail("not_found", $"History entry {Py.Str(historyRef)} not found");
        _state.ProjectHistory.Remove(S(historyRef));
        BumpRevision();
        return OkTrue();
    }
}
