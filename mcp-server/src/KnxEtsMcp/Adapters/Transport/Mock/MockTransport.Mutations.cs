using System.Globalization;
using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    // -- Device from catalog ----------------------------------------------------------

    /// <summary>device.addFromCatalog -- add a device to a line from the catalog.</summary>
    private JsonNode? HandleAddFromCatalog(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        var catalogItemRef = Get(p, "catalogItemRef");
        var address = Get(p, "address");
        if (!Py.Truthy(lineRef) || !Py.Truthy(catalogItemRef) || !Py.Truthy(address))
            throw Fail("invalid_params", "lineRef, catalogItemRef, and address are required");

        // Validate line exists
        if (!AllLines().Any(line => Py.Eq(line["lineRef"], lineRef)))
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");

        // Validate catalog item exists
        var catItem = Find(_catalogItems, catalogItemRef)
            ?? throw Fail("not_found", $"CatalogItem {Py.Str(catalogItemRef)} not found");

        // Check for duplicate address
        foreach (var dev in _state.Devices.Values)
        {
            if (Py.Eq(dev["address"], address))
                throw Fail("invalid_params", $"Device address {Py.Str(address)} already in use");
        }

        var deviceRef = MakeRef("dev");
        _state.Devices[deviceRef] = new JsonObject
        {
            ["ref"] = deviceRef,
            ["address"] = Clone(address),
            ["name"] = Clone(catItem["name"]),
            ["description"] = "",
            ["comment"] = "",
            ["product"] = Clone(GetOr(catItem, "orderNumber", catItem["name"])),
            ["orderNumber"] = Clone(GetOr(catItem, "orderNumber", "")),
            ["line"] = Clone(lineRef),
        };
        BumpRevision();
        return new JsonObject { ["ref"] = deviceRef, ["address"] = Clone(address) };
    }

    // -- Group addresses / links / parameters -------------------------------------------

    private JsonNode? HandleGaCreate(JsonNode? p)
    {
        var name = Get(p, "name");
        var address = Get(p, "address");
        if (!Py.Truthy(name) || !Py.Truthy(address))
            throw Fail("invalid_params", "name and address are required");

        // Check for duplicate address
        foreach (var existing in _state.GroupAddresses.Values)
        {
            if (Py.Eq(existing["address"], address))
                throw Fail("invalid_params", $"Group address {Py.Str(address)} already exists");
        }

        var gaRef = MakeRef("ga");
        JsonNode? dpt = null;
        if (!Py.IsNone(Get(p, "dptMain")))
        {
            var dptText = Py.Str(Get(p, "dptMain"));
            if (!Py.IsNone(Get(p, "dptSub")))
                dptText += "." + Py.FormatD3(Get(p, "dptSub"));
            dpt = dptText;
        }

        _state.GroupAddresses[gaRef] = new JsonObject
        {
            ["ref"] = gaRef,
            ["address"] = Clone(address),
            ["name"] = Clone(name),
            ["dpt"] = dpt,
        };
        BumpRevision();
        return new JsonObject { ["ref"] = gaRef, ["address"] = Clone(address) };
    }

    private JsonNode? HandleLinkCreate(JsonNode? p)
    {
        var coRef = Get(p, "comObjectRef");
        var gaRef = Get(p, "gaRef");
        if (!Py.Truthy(coRef) || !Py.Truthy(gaRef))
            throw Fail("invalid_params", "comObjectRef and gaRef are required");

        var co = Find(_state.ComObjects, coRef)
            ?? throw Fail("not_found", $"ComObject {Py.Str(coRef)} not found");

        if (!Has(_state.GroupAddresses, gaRef))
            throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");

        // 6.3.0 constraint: linking inactive object must fail
        if (!Py.Truthy(GetOr(co, "isActive", true)))
            throw Fail("inactive_object", $"ComObject {Py.Str(coRef)} is inactive and cannot be linked");

        var link = (S(coRef), S(gaRef));
        if (!_links.Contains(link))
            _links.Add(link);
        BumpRevision();

        // Mirror the gateway: return a LinkResult with a soft DPT-compatibility advisory.
        var ga = Find(_state.GroupAddresses, gaRef)!;
        var coDpt = Py.Truthy(co["dpt"]) ? co["dpt"] : null;
        var gaDpt = Py.Truthy(ga["dpt"]) ? ga["dpt"] : null;
        var result = new JsonObject { ["comObjectRef"] = Clone(coRef), ["gaRef"] = Clone(gaRef) };
        if (Py.Truthy(coDpt))
            result["dpt"] = Clone(coDpt);
        if (Py.Truthy(gaDpt))
            result["gaDpt"] = Clone(gaDpt);
        var warning = DptMismatchWarning(coDpt, gaDpt);
        if (!string.IsNullOrEmpty(warning))
            result["dptWarning"] = warning;
        return result;
    }

    private JsonNode? HandleLinkDelete(JsonNode? p)
    {
        var coRef = Get(p, "comObjectRef");
        var gaRef = Get(p, "gaRef");
        if (!Py.Truthy(coRef) || !Py.Truthy(gaRef))
            throw Fail("invalid_params", "comObjectRef and gaRef are required");

        if (!Has(_state.ComObjects, coRef))
            throw Fail("not_found", $"ComObject {Py.Str(coRef)} not found");
        if (!Has(_state.GroupAddresses, gaRef))
            throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");

        // Idempotent: discarding an absent link is not an error. Mirrors the AddIn, which
        // calls ComObjectInstanceRef.Unlink unconditionally. not_found is reserved for
        // unknown comObjectRef/gaRef (see docs/protocol.md).
        _links.Remove((S(coRef), S(gaRef)));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleParamSet(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var parameterRef = Get(p, "parameterRef");
        var value = Get(p, "value");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(parameterRef) || Py.IsNone(value))
            throw Fail("invalid_params", "deviceRef, parameterRef, and value are required");

        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        var param = Find(_state.Parameters, parameterRef)
            ?? throw Fail("not_found", $"Parameter {Py.Str(parameterRef)} not found");

        if (!Py.Eq(param["deviceRef"], deviceRef))
        {
            throw Fail("invalid_params",
                $"Parameter {Py.Str(parameterRef)} does not belong to device {Py.Str(deviceRef)}");
        }

        param["value"] = Clone(value);
        BumpRevision();
        return OkTrue();
    }

    /// <summary>Python <c>{"ok": True}</c>.</summary>
    private static JsonObject OkTrue() => new() { ["ok"] = true };

    // -- Phase A mutation handlers --------------------------------------------------------

    private JsonNode? HandleGaDelete(JsonNode? p)
    {
        var gaRef = Get(p, "groupAddressRef");
        if (!Py.Truthy(gaRef))
            throw Fail("invalid_params", "groupAddressRef is required");
        if (!Has(_state.GroupAddresses, gaRef))
            throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");

        // Remove links referencing this GA
        var key = S(gaRef);
        _links.RemoveAll(l => l.GaRef == key);
        _state.GroupAddresses.Remove(key);
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleGaRename(JsonNode? p)
    {
        var gaRef = Get(p, "groupAddressRef");
        var name = Get(p, "name");
        if (!Py.Truthy(gaRef) || !Py.Truthy(name))
            throw Fail("invalid_params", "groupAddressRef and name are required");
        var ga = Find(_state.GroupAddresses, gaRef)
            ?? throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");

        var (stored, truncated) = ApplyNameLimit(name);
        ga["name"] = stored;
        BumpRevision();
        var result = (JsonObject)ga.DeepClone();
        if (truncated)
            result["truncated"] = true;
        return result;
    }

    private JsonNode? HandleGaSetDescription(JsonNode? p)
    {
        var gaRef = Get(p, "groupAddressRef");
        var description = Get(p, "description");
        if (!Py.Truthy(gaRef) || Py.IsNone(description))
            throw Fail("invalid_params", "groupAddressRef and description are required");
        var ga = Find(_state.GroupAddresses, gaRef)
            ?? throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");

        ga["description"] = Clone(description);
        BumpRevision();
        return ga.DeepClone();
    }

    private JsonNode? HandleGaSetDpt(JsonNode? p)
    {
        var gaRef = Get(p, "groupAddressRef");
        var dptMain = Get(p, "dptMain");
        if (!Py.Truthy(gaRef) || Py.IsNone(dptMain))
            throw Fail("invalid_params", "groupAddressRef and dptMain are required");
        var ga = Find(_state.GroupAddresses, gaRef)
            ?? throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");

        var dptSub = Get(p, "dptSub");
        var dptText = Py.Str(dptMain);
        if (!Py.IsNone(dptSub))
            dptText += "." + Py.FormatD3(dptSub);
        ga["dpt"] = dptText;
        BumpRevision();
        return ga.DeepClone();
    }

    private JsonNode? HandleGroupRangeCreate(JsonNode? p)
    {
        var name = Get(p, "name");
        var address = Get(p, "address");
        if (!Py.Truthy(name) || Py.IsNone(address))
            throw Fail("invalid_params", "name and address are required");

        var parentRef = Get(p, "parentGroupRangeRef");
        if (Py.Truthy(parentRef) && !Has(_state.GroupRanges, parentRef))
            throw Fail("not_found", $"Parent GroupRange {Py.Str(parentRef)} not found");

        var grRef = MakeRef("gr");
        _state.GroupRanges[grRef] = new JsonObject
        {
            ["ref"] = grRef,
            ["name"] = Clone(name),
            ["address"] = Clone(address),
            ["parentRef"] = Clone(parentRef),
        };
        BumpRevision();
        return new JsonObject { ["ref"] = grRef, ["name"] = Clone(name), ["address"] = Clone(address) };
    }

    private JsonNode? HandleGroupRangeDelete(JsonNode? p)
    {
        var grRef = Get(p, "groupRangeRef");
        if (!Py.Truthy(grRef))
            throw Fail("invalid_params", "groupRangeRef is required");
        if (!Has(_state.GroupRanges, grRef))
            throw Fail("not_found", $"GroupRange {Py.Str(grRef)} not found");

        // Remove child ranges (direct children only, like the Python mock)
        var children = _state.GroupRanges
            .Where(kv => Py.Eq(GetOr(kv.Value, "parentRef", null), grRef))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var child in children)
            _state.GroupRanges.Remove(child);
        _state.GroupRanges.Remove(S(grRef));
        BumpRevision();
        return OkTrue();
    }

    // -- Devices -----------------------------------------------------------------------

    private JsonNode? HandleDeviceDelete(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        // Remove com objects and their links
        var coRefs = _state.ComObjects.Values
            .Where(co => Py.Eq(co["deviceRef"], deviceRef))
            .Select(co => S(co["ref"]))
            .ToList();
        foreach (var coRef in coRefs)
        {
            _links.RemoveAll(l => l.CoRef == coRef);
            _state.ComObjects.Remove(coRef);
        }
        // Remove parameters
        var paramRefs = _state.Parameters.Values
            .Where(param => Py.Eq(param["deviceRef"], deviceRef))
            .Select(param => S(param["ref"]))
            .ToList();
        foreach (var paramRef in paramRefs)
            _state.Parameters.Remove(paramRef);
        _state.Devices.Remove(S(deviceRef));
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleDeviceRename(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var name = Get(p, "name");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(name))
            throw Fail("invalid_params", "deviceRef and name are required");
        var device = Find(_state.Devices, deviceRef)
            ?? throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        var (stored, truncated) = ApplyNameLimit(name);
        device["name"] = stored;
        BumpRevision();
        var result = (JsonObject)device.DeepClone();
        if (truncated)
            result["truncated"] = true;
        return result;
    }

    private JsonNode? HandleDeviceSetAddress(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var address = Get(p, "address");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(address))
            throw Fail("invalid_params", "deviceRef and address are required");
        var device = Find(_state.Devices, deviceRef)
            ?? throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        device["address"] = Clone(address);
        BumpRevision();
        return device.DeepClone();
    }

    private JsonNode? HandleDeviceMove(JsonNode? _)
        => throw Fail("not_supported",
            "Moving an assigned device between lines has no supported SDK API "
            + "in ETS 6.3.0. Workaround: delete and re-add the device on the target line.");

    /// <summary>
    /// device.unassign -- detach a device from its line. Moves the device to the
    /// unassigned-devices collection (line=None). The device is NOT deleted.
    /// </summary>
    private JsonNode? HandleDeviceUnassign(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        var device = Find(_state.Devices, deviceRef)
            ?? throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        device["line"] = null;
        BumpRevision();
        return OkTrue();
    }

    // -- Topology (areas / lines) ---------------------------------------------------------

    private JsonNode? HandleAreaCreate(JsonNode? p)
    {
        var name = Get(p, "name");
        if (!Py.Truthy(name))
            throw Fail("invalid_params", "name is required");

        var address = Get(p, "address");
        if (Py.IsNone(address))
        {
            // Auto-allocate: find the next unused address
            var used = Areas().Select(a => a["address"]).ToList();
            for (var i = 0; i < 16; i++)
            {
                var candidate = i.ToString(CultureInfo.InvariantCulture);
                if (!used.Any(u => Py.Eq(u, candidate)))
                {
                    address = JsonValue.Create(i);
                    break;
                }
            }
            if (Py.IsNone(address))
                throw Fail("invalid_params", "No free area address available");
        }

        var area = new JsonObject
        {
            ["areaRef"] = $"area-{Py.Str(address)}",
            ["address"] = Py.Str(address),
            ["name"] = Clone(name),
            ["lines"] = new JsonArray(),
        };
        Push(_state.Topology, area);
        BumpRevision();
        return area.DeepClone();
    }

    private JsonNode? HandleAreaDelete(JsonNode? p)
    {
        var areaRef = Get(p, "areaRef");
        if (!Py.Truthy(areaRef))
            throw Fail("invalid_params", "areaRef is required");

        var index = Areas().ToList().FindIndex(a => Py.Eq(a["areaRef"], areaRef));
        if (index < 0)
            throw Fail("not_found", $"Area {Py.Str(areaRef)} not found");

        _state.Topology.RemoveAt(index);
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleLineCreate(JsonNode? p)
    {
        var areaRef = Get(p, "areaRef");
        var name = Get(p, "name");
        if (!Py.Truthy(areaRef) || !Py.Truthy(name))
            throw Fail("invalid_params", "areaRef and name are required");

        var area = Areas().FirstOrDefault(a => Py.Eq(a["areaRef"], areaRef))
            ?? throw Fail("not_found", $"Area {Py.Str(areaRef)} not found");

        var areaLines = (JsonArray)area["lines"]!;
        var areaAddress = Py.Str(area["address"]);
        var address = Get(p, "address");
        if (Py.IsNone(address))
        {
            var used = areaLines.Select(l => l!["address"]).ToList();
            for (var i = 0; i < 16; i++)
            {
                var candidate = $"{areaAddress}.{i.ToString(CultureInfo.InvariantCulture)}";
                if (!used.Any(u => Py.Eq(u, candidate)))
                {
                    address = JsonValue.Create(i);
                    break;
                }
            }
            // Python leaves address None when all 16 are used -> "line-1.None".
        }

        var line = new JsonObject
        {
            ["lineRef"] = $"line-{areaAddress}.{Py.Str(address)}",
            ["address"] = $"{areaAddress}.{Py.Str(address)}",
            ["name"] = Clone(name),
            ["segments"] = new JsonArray(),
        };
        Push(areaLines, line);
        BumpRevision();
        return line.DeepClone();
    }

    private JsonNode? HandleLineDelete(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        if (!Py.Truthy(lineRef))
            throw Fail("invalid_params", "lineRef is required");

        foreach (var area in Areas())
        {
            var lines = (JsonArray)area["lines"]!;
            for (var i = 0; i < lines.Count; i++)
            {
                if (Py.Eq(lines[i]!["lineRef"], lineRef))
                {
                    lines.RemoveAt(i);
                    BumpRevision();
                    return OkTrue();
                }
            }
        }

        throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");
    }

    // -- Communication objects ------------------------------------------------------------

    private static readonly (string Param, string Flag)[] FlagParams =
    [
        ("communicationFlag", "communication"),
        ("readFlag", "read"),
        ("writeFlag", "write"),
        ("transmitFlag", "transmit"),
        ("updateFlag", "update"),
        ("readOnInitFlag", "readOnInit"),
    ];

    private JsonNode? HandleComObjectSetFlags(JsonNode? p)
    {
        var coRef = Get(p, "comObjectRef");
        if (!Py.Truthy(coRef))
            throw Fail("invalid_params", "comObjectRef is required");
        var co = Find(_state.ComObjects, coRef)
            ?? throw Fail("not_found", $"ComObject {Py.Str(coRef)} not found");

        if (!Py.Truthy(GetOr(co, "isActive", true)))
            throw Fail("inactive_object", $"ComObject {Py.Str(coRef)} is inactive");

        // Flags are applied before the priority is validated; like the Python mock, an
        // invalid priority leaves the flag changes in place (without a revision bump).
        var flags = (JsonObject)co["flags"]!;
        foreach (var (param, flag) in FlagParams)
        {
            var value = Get(p, param);
            if (!Py.IsNone(value))
                flags[flag] = Clone(value);
        }

        var priority = Get(p, "priority");
        if (!Py.IsNone(priority))
        {
            if (!Py.Eq(priority, "Low") && !Py.Eq(priority, "High") && !Py.Eq(priority, "Alert"))
                throw Fail("invalid_params", $"Invalid priority: {Py.Str(priority)}. Valid: Low, High, Alert.");
            co["priority"] = Clone(priority);
        }

        BumpRevision();
        return new JsonObject
        {
            ["ref"] = Clone(coRef),
            ["communicationFlag"] = Clone(GetOr(flags, "communication", true)),
            ["readFlag"] = Clone(GetOr(flags, "read", false)),
            ["writeFlag"] = Clone(GetOr(flags, "write", false)),
            ["transmitFlag"] = Clone(GetOr(flags, "transmit", false)),
            ["updateFlag"] = Clone(GetOr(flags, "update", false)),
            ["readOnInitFlag"] = Clone(GetOr(flags, "readOnInit", false)),
            ["priority"] = Clone(GetOr(co, "priority", "Low")),
        };
    }

    // -- Label setter handlers (v0.1.16) ----------------------------------------------------

    private JsonNode? HandleDeviceSetDescription(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var description = Get(p, "description");
        if (!Py.Truthy(deviceRef) || Py.IsNone(description))
            throw Fail("invalid_params", "deviceRef and description are required");
        var device = Find(_state.Devices, deviceRef)
            ?? throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        device["description"] = Clone(description);
        BumpRevision();
        return device.DeepClone();
    }

    private JsonNode? HandleDeviceSetComment(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var comment = Get(p, "comment");
        if (!Py.Truthy(deviceRef) || Py.IsNone(comment))
            throw Fail("invalid_params", "deviceRef and comment are required");
        var device = Find(_state.Devices, deviceRef)
            ?? throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        device["comment"] = Clone(comment);
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleComObjectSetDescription(JsonNode? p)
    {
        var coRef = Get(p, "comObjectRef");
        var description = Get(p, "description");
        if (!Py.Truthy(coRef) || Py.IsNone(description))
            throw Fail("invalid_params", "comObjectRef and description are required");
        var co = Find(_state.ComObjects, coRef)
            ?? throw Fail("not_found", $"ComObject {Py.Str(coRef)} not found");
        co["description"] = Clone(description);
        BumpRevision();
        return new JsonObject { ["ref"] = Clone(coRef), ["description"] = Clone(description) };
    }

    private JsonNode? HandleComObjectSetFunctionText(JsonNode? p)
    {
        var coRef = Get(p, "comObjectRef");
        var functionText = Get(p, "functionText");
        if (!Py.Truthy(coRef) || Py.IsNone(functionText))
            throw Fail("invalid_params", "comObjectRef and functionText are required");
        var co = Find(_state.ComObjects, coRef)
            ?? throw Fail("not_found", $"ComObject {Py.Str(coRef)} not found");
        co["functionText"] = Clone(functionText);
        BumpRevision();
        return new JsonObject { ["ref"] = Clone(coRef), ["functionText"] = Clone(functionText) };
    }
}
