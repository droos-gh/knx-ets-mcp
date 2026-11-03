using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    // Python raises RecursionError on a parentRef cycle (possible via buildingPart.move);
    // a C# StackOverflowException would kill the test host, so cap the depth instead.
    private const int MaxBuildingDepth = 900;

    private static readonly HashSet<string> ValidBuildingPartTypes = new(StringComparer.Ordinal)
    {
        "Building", "BuildingPart", "Floor", "Room",
        "DistributionBoard", "Corridor", "Stairway",
    };

    // -- Phase B: Building structure handlers -------------------------------------------

    /// <summary>Build a tree node for a building part with children, devices, functions.</summary>
    private JsonObject BuildBpTree(string bpRef, int depth = 0)
    {
        if (depth > MaxBuildingDepth)
            throw Py.Crash("RecursionError: maximum recursion depth exceeded");
        var bp = _state.BuildingParts[bpRef];
        var children = new JsonArray();
        foreach (var child in _state.BuildingParts.Values.ToList())
        {
            if (Py.Eq(GetOr(child, "parentRef", null), bpRef))
                Push(children, BuildBpTree(S(child["ref"]), depth + 1));
        }
        var functions = new JsonArray();
        foreach (var bf in _state.BuildingFunctions.Values)
        {
            if (!Py.Eq(GetOr(bf, "buildingPartRef", null), bpRef))
                continue;
            var gas = new JsonArray();
            foreach (var ga in Py.Iter(GetOr(bf, "groupAddresses", new JsonArray())))
                Push(gas, Clone(ga));
            var entry = new JsonObject
            {
                ["ref"] = Clone(bf["ref"]),
                ["name"] = Clone(bf["name"]),
                ["groupAddresses"] = gas,
            };
            if (gas.Count == 0)
            {
                // Mirror the ETS 6.3.0 stale-empty-list caveat from the gateway.
                entry["note"] =
                    "Empty address list. In ETS 6.3.0 this may be genuinely empty OR "
                    + "a stale SDK cache after a GA was unlinked/deleted; verify in ETS "
                    + "if it matters.";
            }
            Push(functions, entry);
        }
        var devices = new JsonArray();
        foreach (var device in Py.Iter(GetOr(bp, "devices", new JsonArray())))
            Push(devices, Clone(device));
        return new JsonObject
        {
            ["ref"] = Clone(bp["ref"]),
            ["name"] = Clone(bp["name"]),
            ["type"] = Clone(bp["type"]),
            ["children"] = children,
            ["devices"] = devices,
            ["functions"] = functions,
        };
    }

    private JsonNode? HandleBuildingList(JsonNode? _)
    {
        // Return top-level building parts (those with no parent)
        var roots = _state.BuildingParts.Values
            .Where(bp => Py.IsNone(GetOr(bp, "parentRef", null)))
            .Select(bp => S(bp["ref"]))
            .ToList();
        var result = new JsonArray();
        foreach (var root in roots)
            Push(result, BuildBpTree(root));
        return result;
    }

    private JsonNode? HandleBuildingCreate(JsonNode? p)
    {
        var name = Get(p, "name");
        var bpType = Get(p, "type");
        if (!Py.Truthy(name) || !Py.Truthy(bpType))
            throw Fail("invalid_params", "name and type are required");

        if (Py.HashKey(bpType) is not { } typeName || !ValidBuildingPartTypes.Contains(typeName))
            throw Fail("invalid_params", $"Invalid type: {Py.Str(bpType)}");

        var parentRef = Get(p, "parentBuildingPartRef");
        if (Py.Truthy(parentRef) && !Has(_state.BuildingParts, parentRef))
            throw Fail("not_found", $"BuildingPart {Py.Str(parentRef)} not found");

        var bpRef = MakeRef("bp");
        _state.BuildingParts[bpRef] = new JsonObject
        {
            ["ref"] = bpRef,
            ["name"] = Clone(name),
            ["type"] = Clone(bpType),
            ["parentRef"] = Clone(parentRef),
            ["devices"] = new JsonArray(),
        };
        BumpRevision();
        return BuildBpTree(bpRef);
    }

    private JsonNode? HandleBuildingDelete(JsonNode? p) => DeleteBuildingPart(p, 0);

    private JsonObject DeleteBuildingPart(JsonNode? p, int depth)
    {
        if (depth > MaxBuildingDepth)
            throw Py.Crash("RecursionError: maximum recursion depth exceeded");
        var bpRef = Get(p, "buildingPartRef");
        if (!Py.Truthy(bpRef))
            throw Fail("invalid_params", "buildingPartRef is required");
        if (!Has(_state.BuildingParts, bpRef))
            throw Fail("not_found", $"BuildingPart {Py.Str(bpRef)} not found");

        // Delete children recursively (each recursive delete bumps the revision, like Python)
        var children = _state.BuildingParts
            .Where(kv => Py.Eq(GetOr(kv.Value, "parentRef", null), bpRef))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var childRef in children)
            DeleteBuildingPart(new JsonObject { ["buildingPartRef"] = childRef }, depth + 1);
        // Delete associated building functions
        var bfRefs = _state.BuildingFunctions
            .Where(kv => Py.Eq(GetOr(kv.Value, "buildingPartRef", null), bpRef))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var bfRef in bfRefs)
            _state.BuildingFunctions.Remove(bfRef);
        _state.BuildingParts.Remove(S(bpRef));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleBuildingRename(JsonNode? p)
    {
        var bpRef = Get(p, "buildingPartRef");
        var name = Get(p, "name");
        if (!Py.Truthy(bpRef) || !Py.Truthy(name))
            throw Fail("invalid_params", "buildingPartRef and name are required");
        var bp = Find(_state.BuildingParts, bpRef)
            ?? throw Fail("not_found", $"BuildingPart {Py.Str(bpRef)} not found");

        bp["name"] = Clone(name);
        BumpRevision();
        return BuildBpTree(S(bpRef));
    }

    private JsonNode? HandleBuildingAssignDevice(JsonNode? p)
    {
        var bpRef = Get(p, "buildingPartRef");
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(bpRef) || !Py.Truthy(deviceRef))
            throw Fail("invalid_params", "buildingPartRef and deviceRef are required");
        var bp = Find(_state.BuildingParts, bpRef)
            ?? throw Fail("not_found", $"BuildingPart {Py.Str(bpRef)} not found");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        // Python setdefault("devices", [])
        if (!bp.TryGetPropertyValue("devices", out var devicesNode))
        {
            devicesNode = new JsonArray();
            bp["devices"] = devicesNode;
        }
        var devices = (JsonArray)devicesNode!;
        if (!Py.ListContains(devices, deviceRef))
            Push(devices, Clone(deviceRef));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleBuildingUnassignDevice(JsonNode? p)
    {
        var bpRef = Get(p, "buildingPartRef");
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(bpRef) || !Py.Truthy(deviceRef))
            throw Fail("invalid_params", "buildingPartRef and deviceRef are required");
        var bp = Find(_state.BuildingParts, bpRef)
            ?? throw Fail("not_found", $"BuildingPart {Py.Str(bpRef)} not found");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        if (bp["devices"] is JsonArray devices)
        {
            var index = Py.IndexOf(devices, deviceRef);
            if (index >= 0)
                devices.RemoveAt(index);
        }
        BumpRevision();
        return OkTrue();
    }

    // -- Phase B: Building function handlers -----------------------------------------------

    private JsonNode? HandleBfCreate(JsonNode? p)
    {
        var bpRef = Get(p, "buildingPartRef");
        var name = Get(p, "name");
        if (!Py.Truthy(bpRef) || !Py.Truthy(name))
            throw Fail("invalid_params", "buildingPartRef and name are required");
        if (!Has(_state.BuildingParts, bpRef))
            throw Fail("not_found", $"BuildingPart {Py.Str(bpRef)} not found");

        var bfRef = MakeRef("bf");
        _state.BuildingFunctions[bfRef] = new JsonObject
        {
            ["ref"] = bfRef,
            ["name"] = Clone(name),
            ["buildingPartRef"] = Clone(bpRef),
            ["groupAddresses"] = new JsonArray(),
        };
        BumpRevision();
        return new JsonObject { ["ref"] = bfRef, ["name"] = Clone(name), ["groupAddresses"] = new JsonArray() };
    }

    private JsonNode? HandleBfDelete(JsonNode? p)
    {
        var bfRef = Get(p, "buildingFunctionRef");
        if (!Py.Truthy(bfRef))
            throw Fail("invalid_params", "buildingFunctionRef is required");
        if (!Has(_state.BuildingFunctions, bfRef))
            throw Fail("not_found", $"BuildingFunction {Py.Str(bfRef)} not found");

        _state.BuildingFunctions.Remove(S(bfRef));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleBfLinkGa(JsonNode? p)
    {
        var bfRef = Get(p, "buildingFunctionRef");
        var gaRefs = Get(p, "groupAddressRefs");
        if (!Py.Truthy(bfRef) || !Py.Truthy(gaRefs))
            throw Fail("invalid_params", "buildingFunctionRef and groupAddressRefs are required");
        var bf = Find(_state.BuildingFunctions, bfRef)
            ?? throw Fail("not_found", $"BuildingFunction {Py.Str(bfRef)} not found");

        // Validated and applied per element: an unknown GA after valid ones leaves the
        // earlier ones linked (no revision bump), exactly like the Python loop.
        var linked = (JsonArray)bf["groupAddresses"]!;
        foreach (var gaRef in Py.Iter(gaRefs))
        {
            if (!Has(_state.GroupAddresses, gaRef))
                throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
            if (!Py.ListContains(linked, gaRef))
                Push(linked, Clone(gaRef));
        }
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleBfUnlinkGa(JsonNode? p)
    {
        var bfRef = Get(p, "buildingFunctionRef");
        var gaRefs = Get(p, "groupAddressRefs");
        if (!Py.Truthy(bfRef) || !Py.Truthy(gaRefs))
            throw Fail("invalid_params", "buildingFunctionRef and groupAddressRefs are required");
        var bf = Find(_state.BuildingFunctions, bfRef)
            ?? throw Fail("not_found", $"BuildingFunction {Py.Str(bfRef)} not found");

        var linked = (JsonArray)bf["groupAddresses"]!;
        foreach (var gaRef in Py.Iter(gaRefs))
        {
            if (!Has(_state.GroupAddresses, gaRef))
                throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
            var index = Py.IndexOf(linked, gaRef);
            if (index >= 0)
                linked.RemoveAt(index);
        }
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleBuildingPartMove(JsonNode? p)
    {
        var targetRef = Get(p, "targetBuildingPartRef");
        var sourceRef = Get(p, "sourceBuildingPartRef");
        if (!Py.Truthy(targetRef) || !Py.Truthy(sourceRef))
            throw Fail("invalid_params", "targetBuildingPartRef and sourceBuildingPartRef are required");
        if (!Has(_state.BuildingParts, targetRef))
            throw Fail("not_found", $"BuildingPart {Py.Str(targetRef)} not found");
        var source = Find(_state.BuildingParts, sourceRef)
            ?? throw Fail("not_found", $"BuildingPart {Py.Str(sourceRef)} not found");
        source["parentRef"] = Clone(targetRef);
        BumpRevision();
        return OkTrue();
    }
}
