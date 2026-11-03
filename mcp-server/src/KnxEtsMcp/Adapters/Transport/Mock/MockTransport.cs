using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic;

namespace KnxEtsMcp.Adapters.Transport;

/// <summary>
/// In-process fake of the ETS AddIn: processes protocol requests (docs/protocol.md)
/// against an in-memory project model with a few devices, group addresses and
/// communication objects. Used for tests and development where no ETS is available.
/// </summary>
/// <remarks>
/// A 1:1 port of the Python <c>knx_ets_mcp/transport/mock.py</c>: every handler, check
/// order, error code/message, result field and seed value matches it. Entities are kept as
/// <see cref="JsonObject"/>s in insertion-ordered maps (Python dicts preserve insertion
/// order); Python value semantics live in the nested <c>Py</c> helper. Results never alias
/// internal state.
/// </remarks>
public sealed partial class MockTransport : ITransport
{
    /// <summary>The session token the mock accepts (Python <c>MOCK_TOKEN</c>).</summary>
    public const string MockToken = "mock-token-for-testing";

    // Python _MAX_BATCH_OPERATIONS.
    private const int MaxBatchOperations = 500;

    // Mock-only simulated name-length limit, so truncation reporting is testable
    // (Python _MOCK_MAX_NAME_LEN). Real ETS enforces per-entity MaxNameLength.
    private const int MockMaxNameLength = 40;

    private readonly Dictionary<string, Func<JsonNode?, JsonNode?>> _handlers;
    private bool _connected;

    // State covered by the batch.apply snapshot (Python _MOCK_STATE_ATTRS).
    private ProjectState _state = new();

    // State NOT covered by the snapshot -- exactly the Python attributes missing from
    // _MOCK_STATE_ATTRS. Notably the com-object links: an atomic batch rollback does not
    // undo link.create/link.delete steps in the Python mock, and neither does this port.
    // Python keeps the links in a set; a list with set semantics keeps them in insertion
    // order (the Python iteration order is hash-based and therefore unspecified).
    private readonly List<(string CoRef, string GaRef)> _links = [];
    private readonly OrderedDictionary<string, JsonObject> _jobs = new();
    private readonly OrderedDictionary<string, JsonObject> _manufacturers = new();
    private readonly OrderedDictionary<string, JsonObject> _catalogItems = new();
    private readonly OrderedDictionary<string, JsonObject> _unifiedCatalogItems = new();
    private readonly OrderedDictionary<string, JsonArray> _importablePaths = new();
    private readonly Dictionary<string, JsonNode?> _idempotencyCache = new(StringComparer.Ordinal);

    /// <summary>Creates the mock with its seed project.</summary>
    /// <param name="busReadOnly">Initial value of <see cref="BusReadOnly"/>.</param>
    public MockTransport(bool busReadOnly = false)
    {
        BusReadOnly = busReadOnly;
        _handlers = BuildHandlers();
        SeedData();
    }

    /// <summary>
    /// Simulates the AddIn's "Bus read-only" preference (default off): bus writes
    /// (<see cref="ProtocolMethods.BusWrites"/>) are refused with <c>approval_required</c>.
    /// </summary>
    public bool BusReadOnly { get; set; }

    /// <summary>Current project revision (Python <c>_revision</c>); tests read and set it.</summary>
    public string Revision
    {
        get => _state.Revision;
        set => _state.Revision = value;
    }

    /// <inheritdoc />
    public string Token => MockToken;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes one request (Python <c>send()</c>) and returns the response envelope:
    /// <c>{"id","ok":true,"result","projectRevision"}</c> or
    /// <c>{"id","ok":false,"error":{"code","message"}}</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Not connected ("MockTransport not connected"), or the request makes the Python mock
    /// crash (an uncaught TypeError/ValueError/AttributeError there).
    /// </exception>
    public Task<JsonNode?> SendAsync(JsonObject request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<JsonNode?>(cancellationToken);
        try
        {
            return Task.FromResult<JsonNode?>(Send(request));
        }
        catch (Exception ex)
        {
            return Task.FromException<JsonNode?>(ex);
        }
    }

    /// <inheritdoc />
    public Task CloseAsync()
    {
        _connected = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }

    private JsonObject Send(JsonObject request)
    {
        if (!_connected)
            throw new InvalidOperationException("MockTransport not connected");

        JsonNode? reqId = request.TryGetPropertyValue("id", out var idNode) ? idNode?.DeepClone() : "unknown";
        var reqToken = request["token"];
        JsonNode? method = request.TryGetPropertyValue("method", out var methodNode) ? methodNode : "";
        JsonNode? parameters = request.TryGetPropertyValue("params", out var paramsNode) ? paramsNode : new JsonObject();
        var idempotencyKey = request["idempotencyKey"];
        var expectedRev = request["expectedProjectRevision"];

        // Token validation
        if (!Py.Eq(reqToken, MockToken))
            return Error(reqId, "invalid_token", "Token mismatch");

        // Idempotency: return cached result BEFORE revision check, so a lost-response
        // retry with the original expectedProjectRevision still replays the cached success.
        string? cacheKey = null;
        if (Py.Truthy(idempotencyKey))
        {
            cacheKey = Py.AnyHashKey(idempotencyKey);
            if (_idempotencyCache.TryGetValue(cacheKey, out var cached))
                return Ok(reqId, cached?.DeepClone());
        }

        // Bus-write gate (after idempotency replay, before the revision check -- same order
        // as the AddIn's IpcServer).
        if (BusReadOnly && Py.HashKey(method) is { } busKey && ProtocolMethods.BusWrites.Contains(busKey))
        {
            return Error(reqId, "approval_required",
                $"{busKey} writes to the KNX bus, but the AddIn is in 'Bus read-only' "
                + "mode. Ask the user to disable 'Bus read-only' in the AddIn "
                + "configuration if this operation is intended.");
        }

        // Revision check (after idempotency replay)
        if (!Py.IsNone(expectedRev) && !Py.Eq(expectedRev, _state.Revision))
            return Error(reqId, "revision_mismatch", $"Expected {Py.Str(expectedRev)}, current {_state.Revision}");

        // Route to handler
        if (Py.HashKey(method) is not { } methodName || !_handlers.TryGetValue(methodName, out var handler))
            return Error(reqId, "invalid_params", $"Unknown method: {Py.Str(method)}");

        JsonNode? result;
        try
        {
            result = handler(parameters);
        }
        catch (MockProtocolException ex)
        {
            return Error(reqId, ex.Code, ex.Message);
        }

        // Cache mutating results
        if (cacheKey is not null)
            _idempotencyCache[cacheKey] = result?.DeepClone();

        return Ok(reqId, result);
    }

    private Dictionary<string, Func<JsonNode?, JsonNode?>> BuildHandlers() => new(StringComparer.Ordinal)
    {
        ["bridge.info"] = HandleBridgeInfo,
        ["project.info"] = HandleProjectInfo,
        ["devices.list"] = HandleDevicesList,
        ["ga.list"] = HandleGaList,
        ["comobjects.list"] = HandleComObjectsList,
        ["topology.list"] = HandleTopologyList,
        ["catalog.manufacturers"] = HandleCatalogManufacturers,
        ["catalog.search"] = HandleCatalogSearch,
        ["catalog.search_online"] = HandleCatalogSearchOnline,
        ["catalog.import"] = HandleCatalogImport,
        ["catalog.internalize"] = HandleCatalogInternalize,
        ["params.list"] = HandleParamsList,
        ["ga.create"] = HandleGaCreate,
        ["link.create"] = HandleLinkCreate,
        ["link.delete"] = HandleLinkDelete,
        ["param.set"] = HandleParamSet,
        ["batch.apply"] = HandleBatchApply,
        ["ga.delete"] = HandleGaDelete,
        ["ga.rename"] = HandleGaRename,
        ["ga.setDescription"] = HandleGaSetDescription,
        ["ga.setDatapointType"] = HandleGaSetDpt,
        ["groupRange.create"] = HandleGroupRangeCreate,
        ["groupRange.delete"] = HandleGroupRangeDelete,
        ["device.delete"] = HandleDeviceDelete,
        ["device.rename"] = HandleDeviceRename,
        ["device.setAddress"] = HandleDeviceSetAddress,
        ["device.move"] = HandleDeviceMove,
        ["area.create"] = HandleAreaCreate,
        ["area.delete"] = HandleAreaDelete,
        ["line.create"] = HandleLineCreate,
        ["line.delete"] = HandleLineDelete,
        ["comObject.setFlags"] = HandleComObjectSetFlags,
        ["device.setDescription"] = HandleDeviceSetDescription,
        ["device.setComment"] = HandleDeviceSetComment,
        ["comObject.setDescription"] = HandleComObjectSetDescription,
        ["comObject.setFunctionText"] = HandleComObjectSetFunctionText,
        ["device.addFromCatalog"] = HandleAddFromCatalog,
        ["device.program"] = HandleDeviceProgram,
        ["firmware.update"] = HandleFirmwareUpdate,
        ["job.status"] = HandleJobStatus,
        ["job.cancel"] = HandleJobCancel,
        // Phase B: Building structure + functions
        ["building.list"] = HandleBuildingList,
        ["building.create"] = HandleBuildingCreate,
        ["building.delete"] = HandleBuildingDelete,
        ["building.rename"] = HandleBuildingRename,
        ["building.assignDevice"] = HandleBuildingAssignDevice,
        ["building.unassignDevice"] = HandleBuildingUnassignDevice,
        ["buildingFunction.create"] = HandleBfCreate,
        ["buildingFunction.delete"] = HandleBfDelete,
        ["buildingFunction.linkGroupAddress"] = HandleBfLinkGa,
        ["buildingFunction.unlinkGroupAddress"] = HandleBfUnlinkGa,
        // Phase C: Project management
        ["project.save"] = HandleProjectSave,
        ["project.export"] = HandleProjectExport,
        ["project.backup"] = HandleProjectBackup,
        ["project.undo"] = HandleProjectUndo,
        ["project.redo"] = HandleProjectRedo,
        ["project.exportSemantic"] = HandleProjectExportSemantic,
        // Phase E: Catalog browsing
        ["catalog.browseProducts"] = HandleCatalogBrowseProducts,
        ["catalog.productInfo"] = HandleCatalogProductInfo,
        // Phase D: Bus / Online operations
        ["bus.ping"] = HandleBusPing,
        ["bus.scanLine"] = HandleBusScanLine,
        ["bus.reconstructLine"] = HandleBusReconstructLine,
        ["device.readInfo"] = HandleDeviceReadInfo,
        ["device.compare"] = HandleDeviceCompare,
        ["device.readGroupObjects"] = HandleDeviceReadGroupObjects,
        ["group.read"] = HandleGroupRead,
        ["group.write"] = HandleGroupWrite,
        ["group.monitor"] = HandleGroupMonitor,
        ["device.unload"] = HandleDeviceUnload,
        ["bus.setIndividualAddress"] = HandleBusSetIndividualAddress,
        // Reconciliation
        ["device.unassign"] = HandleDeviceUnassign,
        // Phase G: Bus writes
        ["device.reset"] = HandleDeviceReset,
        // Phase G: Moves
        ["groupRange.moveGroupAddress"] = HandleMoveGroupAddress,
        ["groupRange.moveGroupRange"] = HandleMoveGroupRange,
        ["line.move"] = HandleLineMove,
        ["buildingPart.move"] = HandleBuildingPartMove,
        // Phase G: Additional addresses
        ["device.addAdditionalAddress"] = HandleDeviceAddAdditionalAddress,
        ["device.removeAdditionalAddress"] = HandleDeviceRemoveAdditionalAddress,
        ["line.addAdditionalGroupAddress"] = HandleLineAddAdditionalGa,
        ["line.removeAdditionalGroupAddress"] = HandleLineRemoveAdditionalGa,
        // Phase G: Segments
        ["segment.create"] = HandleSegmentCreate,
        ["segment.delete"] = HandleSegmentDelete,
        // Phase G: Trades
        ["trades.list"] = HandleTradesList,
        ["trade.create"] = HandleTradeCreate,
        ["trade.delete"] = HandleTradeDelete,
        ["trade.assignDevice"] = HandleTradeAssignDevice,
        ["trade.unassignDevice"] = HandleTradeUnassignDevice,
        // Phase G: Bus interface / filter
        ["busInterface.link"] = HandleBusInterfaceLink,
        ["busInterface.unlink"] = HandleBusInterfaceUnlink,
        // Phase G: Parameter default
        ["param.setDefault"] = HandleParamSetDefault,
        // Phase G: Certificates
        ["certificates.list"] = HandleCertificatesList,
        ["certificate.add"] = HandleCertificateAdd,
        ["certificate.delete"] = HandleCertificateDelete,
        // Phase G: Navigation
        ["project.navigateTo"] = HandleNavigateTo,
        // Phase G: Tags
        ["tags.list"] = HandleTagsList,
        ["tag.create"] = HandleTagCreate,
        ["tag.delete"] = HandleTagDelete,
        // Phase G: ToDos
        ["todos.list"] = HandleTodosList,
        ["todo.create"] = HandleTodoCreate,
        ["todo.delete"] = HandleTodoDelete,
        // Phase G: Channels/modules
        ["device.channels"] = HandleDeviceChannels,
        ["application.dynamic"] = HandleApplicationDynamic,
        // Phase G: Project history
        ["projectHistory.list"] = HandleProjectHistoryList,
        ["projectHistory.add"] = HandleProjectHistoryAdd,
        ["projectHistory.delete"] = HandleProjectHistoryDelete,
    };

    // -- Envelopes / shared helpers ------------------------------------------------

    private JsonObject Ok(JsonNode? reqId, JsonNode? result) => new()
    {
        ["id"] = reqId,
        ["ok"] = true,
        ["result"] = result,
        ["projectRevision"] = _state.Revision,
    };

    private static JsonObject Error(JsonNode? reqId, string code, string message) => new()
    {
        ["id"] = reqId,
        ["ok"] = false,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    private void BumpRevision()
    {
        _state.RevisionCounter++;
        _state.Revision = $"rev-{_state.RevisionCounter:D3}";
    }

    /// <summary>Python <c>_make_ref</c>: <c>{prefix}-{uuid4().hex[:8]}</c>.</summary>
    private static string MakeRef(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Python <c>str(uuid.uuid4())</c> for job ids.</summary>
    private static string NewJobId() => Guid.NewGuid().ToString();

    /// <summary>Python <c>_ProtocolError(code, message)</c>.</summary>
    private static MockProtocolException Fail(string code, string message) => new(code, message);

    /// <summary>
    /// Handler params as a dict; Python calls <c>params.get(...)</c>, which crashes with an
    /// AttributeError when params is not a dict.
    /// </summary>
    private static JsonObject AsDict(JsonNode? p)
        => p as JsonObject ?? throw Py.Crash($"AttributeError: '{Py.TypeName(p)}' object has no attribute 'get'");

    /// <summary>Python <c>d.get(key)</c> (missing and JSON null both give None).</summary>
    private static JsonNode? Get(JsonNode? p, string key) => AsDict(p)[key];

    /// <summary>Python <c>d.get(key, default)</c> (an explicit JSON null is returned as None).</summary>
    private static JsonNode? GetOr(JsonNode? p, string key, JsonNode? fallback)
        => AsDict(p).TryGetPropertyValue(key, out var v) ? v : fallback;

    /// <summary>A deep copy (Python <c>copy.deepcopy</c>), also used before re-parenting a node.</summary>
    private static JsonNode? Clone(JsonNode? n) => n?.DeepClone();

    /// <summary>Appends via the non-generic <c>JsonArray.Add(JsonNode?)</c>, never the generic reflection-based overload.</summary>
    private static void Push(JsonArray array, JsonNode? item) => array.Add(item);

    /// <summary>An internal string field (refs etc. are always strings).</summary>
    private static string S(JsonNode? n) => Py.RequireStr(n, "str");

    /// <summary>Python <c>d.get(key)</c> on a str-keyed map.</summary>
    private static T? Find<T>(OrderedDictionary<string, T> map, JsonNode? key)
        where T : class
        => Py.HashKey(key) is { } k && map.TryGetValue(k, out var v) ? v : null;

    /// <summary>Python <c>key in d</c> on a str-keyed map.</summary>
    private static bool Has<T>(OrderedDictionary<string, T> map, JsonNode? key)
        where T : class
        => Find(map, key) is not null;

    /// <summary>Deep copies of the values, as a JSON list.</summary>
    private static JsonArray CopyList(IEnumerable<JsonObject> items)
    {
        var list = new JsonArray();
        foreach (var item in items)
            Push(list, item.DeepClone());
        return list;
    }

    /// <summary>All lines of all areas (Python <c>for area in topology: for line in area.get("lines", [])</c>).</summary>
    private IEnumerable<JsonObject> AllLines()
    {
        foreach (var area in _state.Topology)
        {
            if (GetOr(area, "lines", null) is JsonArray lines)
            {
                foreach (var line in lines)
                    yield return (JsonObject)line!;
            }
        }
    }

    private IEnumerable<JsonObject> Areas() => _state.Topology.Select(a => (JsonObject)a!);

    /// <summary>Python <c>_apply_name_limit</c>: simulate ETS truncating a too-long name.</summary>
    private static (string Stored, bool Truncated) ApplyNameLimit(JsonNode? nameNode)
    {
        // len()/slicing of a non-str crashes in Python.
        var name = Py.RequireStr(nameNode, "len");
        return Py.Len(name) > MockMaxNameLength
            ? (Py.Prefix(name, MockMaxNameLength), true)
            : (name, false);
    }

    /// <summary>
    /// Python <c>_dpt_mismatch_warning</c>: warns only when both DPTs are set and their MAIN
    /// numbers differ (e.g. 1.x switch vs 5.x percent).
    /// </summary>
    private static string? DptMismatchWarning(JsonNode? coDpt, JsonNode? gaDpt)
    {
        if (!Py.Truthy(coDpt) || !Py.Truthy(gaDpt))
            return null;
        static string MainNumber(JsonNode? s)
        {
            var text = Py.RequireStr(s, "split");
            var dot = text.IndexOf('.', StringComparison.Ordinal);
            return dot < 0 ? text : text[..dot];
        }
        if (!string.Equals(MainNumber(coDpt).ToLowerInvariant(), MainNumber(gaDpt).ToLowerInvariant(), StringComparison.Ordinal))
        {
            return $"DPT mismatch: com-object is {Py.Str(coDpt)} but group address is {Py.Str(gaDpt)}. "
                + "They will not exchange data correctly -- align the DPTs.";
        }
        return null;
    }

    /// <summary>
    /// Python <c>_normalize_individual_address</c>: accepts '1.1.5', '1, 1, 5', ' 1 , 1 , 5 '
    /// etc. and normalises to '1.1.5'.
    /// </summary>
    private static string NormalizeIndividualAddress(JsonNode? rawNode)
    {
        var raw = Py.RequireStr(rawNode, "__contains__");
        var sep = raw.Contains(',', StringComparison.Ordinal) ? ',' : '.';
        var parts = raw.Split(sep).Select(p => p.Trim()).ToList();
        if (parts.Count != 3)
            throw Fail("invalid_params", $"Invalid address format: '{raw}'");
        var nums = new List<string>(3);
        foreach (var part in parts)
        {
            var n = Py.ParseInt(part) ?? throw Fail("invalid_params", $"Invalid address format: '{raw}'");
            nums.Add(n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return $"{nums[0]}.{nums[1]}.{nums[2]}";
    }

    /// <summary>Python <c>_ProtocolError</c>: a protocol error turned into an error envelope.</summary>
    private sealed class MockProtocolException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    /// <summary>
    /// The mutable state snapshotted for atomic batch rollback -- exactly the Python
    /// <c>_MOCK_STATE_ATTRS</c>.
    /// </summary>
    private sealed class ProjectState
    {
        public string Revision { get; set; } = "rev-001";
        public int RevisionCounter { get; set; } = 1;
        public JsonObject Project { get; set; } = new();
        public OrderedDictionary<string, JsonObject> Devices { get; set; } = new();
        public OrderedDictionary<string, JsonObject> GroupAddresses { get; set; } = new();
        public OrderedDictionary<string, JsonObject> ComObjects { get; set; } = new();
        public OrderedDictionary<string, JsonObject> Parameters { get; set; } = new();
        /// <summary>List of areas (each with nested lines/segments).</summary>
        public JsonArray Topology { get; set; } = new();
        public OrderedDictionary<string, JsonObject> GroupRanges { get; set; } = new();
        /// <summary>Flat index; tree via parentRef.</summary>
        public OrderedDictionary<string, JsonObject> BuildingParts { get; set; } = new();
        public OrderedDictionary<string, JsonObject> BuildingFunctions { get; set; } = new();
        public OrderedDictionary<string, JsonObject> Trades { get; set; } = new();
        public int TradeCounter { get; set; }
        public OrderedDictionary<string, JsonObject> Tags { get; set; } = new();
        public int TagCounter { get; set; }
        public OrderedDictionary<string, JsonObject> Todos { get; set; } = new();
        public int TodoCounter { get; set; }
        public OrderedDictionary<string, JsonObject> Certificates { get; set; } = new();
        /// <summary>deviceRef -> list of additional addresses (as given, usually ints).</summary>
        public OrderedDictionary<string, JsonArray> AdditionalDeviceAddresses { get; set; } = new();
        /// <summary>lineRef -> list of gaRefs.</summary>
        public OrderedDictionary<string, JsonArray> AdditionalLineGas { get; set; } = new();
        /// <summary>deviceRef -> list of gaRefs.</summary>
        public OrderedDictionary<string, JsonArray> BusInterfaceLinks { get; set; } = new();
        public OrderedDictionary<string, JsonObject> ProjectHistory { get; set; } = new();
        public int HistoryCounter { get; set; }
        /// <summary>segmentRef -> {segmentRef, name, lineRef, mediumType}.</summary>
        public OrderedDictionary<string, JsonObject> Segments { get; set; } = new();

        /// <summary>Python <c>_snapshot_state</c>: a deep copy of every attribute.</summary>
        public ProjectState DeepCopy() => new()
        {
            Revision = Revision,
            RevisionCounter = RevisionCounter,
            Project = (JsonObject)Project.DeepClone(),
            Devices = CopyMap(Devices),
            GroupAddresses = CopyMap(GroupAddresses),
            ComObjects = CopyMap(ComObjects),
            Parameters = CopyMap(Parameters),
            Topology = (JsonArray)Topology.DeepClone(),
            GroupRanges = CopyMap(GroupRanges),
            BuildingParts = CopyMap(BuildingParts),
            BuildingFunctions = CopyMap(BuildingFunctions),
            Trades = CopyMap(Trades),
            TradeCounter = TradeCounter,
            Tags = CopyMap(Tags),
            TagCounter = TagCounter,
            Todos = CopyMap(Todos),
            TodoCounter = TodoCounter,
            Certificates = CopyMap(Certificates),
            AdditionalDeviceAddresses = CopyMap(AdditionalDeviceAddresses),
            AdditionalLineGas = CopyMap(AdditionalLineGas),
            BusInterfaceLinks = CopyMap(BusInterfaceLinks),
            ProjectHistory = CopyMap(ProjectHistory),
            HistoryCounter = HistoryCounter,
            Segments = CopyMap(Segments),
        };

        private static OrderedDictionary<string, T> CopyMap<T>(OrderedDictionary<string, T> source)
            where T : JsonNode
        {
            var copy = new OrderedDictionary<string, T>();
            foreach (var (key, value) in source)
                copy.Add(key, (T)value.DeepClone());
            return copy;
        }
    }
}
