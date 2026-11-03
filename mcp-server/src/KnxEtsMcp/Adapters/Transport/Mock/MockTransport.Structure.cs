using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    // -- Phase G: Move handlers --------------------------------------------------------------

    private JsonNode? HandleMoveGroupAddress(JsonNode? p)
    {
        var targetRef = Get(p, "targetGroupRangeRef");
        var gaRef = Get(p, "groupAddressRef");
        if (!Py.Truthy(targetRef) || !Py.Truthy(gaRef))
            throw Fail("invalid_params", "targetGroupRangeRef and groupAddressRef are required");
        if (!Has(_state.GroupRanges, targetRef))
            throw Fail("not_found", $"GroupRange {Py.Str(targetRef)} not found");
        if (!Has(_state.GroupAddresses, gaRef))
            throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleMoveGroupRange(JsonNode? p)
    {
        var targetRef = Get(p, "targetGroupRangeRef");
        var sourceRef = Get(p, "sourceGroupRangeRef");
        if (!Py.Truthy(targetRef) || !Py.Truthy(sourceRef))
            throw Fail("invalid_params", "targetGroupRangeRef and sourceGroupRangeRef are required");
        if (!Has(_state.GroupRanges, targetRef))
            throw Fail("not_found", $"GroupRange {Py.Str(targetRef)} not found");
        var source = Find(_state.GroupRanges, sourceRef)
            ?? throw Fail("not_found", $"GroupRange {Py.Str(sourceRef)} not found");
        source["parentRef"] = Clone(targetRef);
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleLineMove(JsonNode? p)
    {
        var targetAreaRef = Get(p, "targetAreaRef");
        var lineRef = Get(p, "lineRef");
        if (!Py.Truthy(targetAreaRef) || !Py.Truthy(lineRef))
            throw Fail("invalid_params", "targetAreaRef and lineRef are required");
        var targetArea = Areas().FirstOrDefault(a => Py.Eq(a["areaRef"], targetAreaRef))
            ?? throw Fail("not_found", $"Area {Py.Str(targetAreaRef)} not found");

        // Find and remove the line from its current area
        JsonNode? movedLine = null;
        foreach (var area in Areas())
        {
            var lines = (JsonArray)area["lines"]!;
            for (var i = 0; i < lines.Count; i++)
            {
                if (Py.Eq(lines[i]!["lineRef"], lineRef))
                {
                    movedLine = lines[i];
                    lines.RemoveAt(i); // detaches the node, so it can be re-parented below
                    break;
                }
            }
            if (movedLine is not null)
                break;
        }
        if (movedLine is null)
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");
        Push((JsonArray)targetArea["lines"]!, movedLine);
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: Additional address handlers -----------------------------------------------

    private JsonNode? HandleDeviceAddAdditionalAddress(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var address = Get(p, "address");
        if (!Py.Truthy(deviceRef) || Py.IsNone(address))
            throw Fail("invalid_params", "deviceRef and address are required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        // Python setdefault: the (possibly empty) entry is created even when the add fails.
        var addresses = SetDefaultList(_state.AdditionalDeviceAddresses, S(deviceRef));
        if (Py.ListContains(addresses, address))
            throw Fail("invalid_params", $"Additional address {Py.Str(address)} already exists");
        Push(addresses, Clone(address));
        BumpRevision();
        return new JsonObject { ["deviceRef"] = Clone(deviceRef), ["address"] = Clone(address) };
    }

    private JsonNode? HandleDeviceRemoveAdditionalAddress(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var address = Get(p, "address");
        if (!Py.Truthy(deviceRef) || Py.IsNone(address))
            throw Fail("invalid_params", "deviceRef and address are required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        var addresses = Find(_state.AdditionalDeviceAddresses, deviceRef) ?? new JsonArray();
        var index = Py.IndexOf(addresses, address);
        if (index < 0)
            throw Fail("not_found", $"Additional address {Py.Str(address)} not found");
        addresses.RemoveAt(index);
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleLineAddAdditionalGa(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        var gaRefs = Get(p, "groupAddressRefs");
        if (!Py.Truthy(lineRef) || !Py.Truthy(gaRefs))
            throw Fail("invalid_params", "lineRef and groupAddressRefs are required");
        // Validate line exists
        if (!AllLines().Any(line => Py.Eq(line["lineRef"], lineRef)))
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");
        var refs = Py.Iter(gaRefs);
        foreach (var gaRef in refs)
        {
            if (!Has(_state.GroupAddresses, gaRef))
                throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
        }
        var addresses = SetDefaultList(_state.AdditionalLineGas, S(lineRef));
        foreach (var gaRef in refs)
        {
            if (!Py.ListContains(addresses, gaRef))
                Push(addresses, Clone(gaRef));
        }
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleLineRemoveAdditionalGa(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        var gaRefs = Get(p, "groupAddressRefs");
        if (!Py.Truthy(lineRef) || !Py.Truthy(gaRefs))
            throw Fail("invalid_params", "lineRef and groupAddressRefs are required");
        if (!AllLines().Any(line => Py.Eq(line["lineRef"], lineRef)))
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");
        var addresses = Find(_state.AdditionalLineGas, lineRef) ?? new JsonArray();
        foreach (var gaRef in Py.Iter(gaRefs))
        {
            var index = Py.IndexOf(addresses, gaRef);
            if (index >= 0)
                addresses.RemoveAt(index);
        }
        BumpRevision();
        return OkTrue();
    }

    /// <summary>Python <c>d.setdefault(key, [])</c>.</summary>
    private static JsonArray SetDefaultList(OrderedDictionary<string, JsonArray> map, string key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new JsonArray();
            map[key] = list;
        }
        return list;
    }

    // -- Phase G: Segment handlers ------------------------------------------------------------

    private JsonNode? HandleSegmentCreate(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        var name = Get(p, "name");
        var mediumType = Get(p, "mediumType");
        if (!Py.Truthy(lineRef) || !Py.Truthy(name) || !Py.Truthy(mediumType))
            throw Fail("invalid_params", "lineRef, name, and mediumType are required");
        // Find the line
        var targetLine = AllLines().FirstOrDefault(line => Py.Eq(line["lineRef"], lineRef))
            ?? throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");
        var segRef = MakeRef("seg");
        Push((JsonArray)targetLine["segments"]!, new JsonObject { ["segmentRef"] = segRef, ["name"] = Clone(name) });
        _state.Segments[segRef] = new JsonObject
        {
            ["segmentRef"] = segRef,
            ["name"] = Clone(name),
            ["lineRef"] = Clone(lineRef),
            ["mediumType"] = Clone(mediumType),
        };
        BumpRevision();
        return new JsonObject { ["segmentRef"] = segRef, ["name"] = Clone(name) };
    }

    private JsonNode? HandleSegmentDelete(JsonNode? p)
    {
        var segRef = Get(p, "segmentRef");
        if (!Py.Truthy(segRef))
            throw Fail("invalid_params", "segmentRef is required");
        if (!Has(_state.Segments, segRef))
            throw Fail("not_found", $"Segment {Py.Str(segRef)} not found");
        // Remove from the line's segments list
        foreach (var line in AllLines())
        {
            var segments = (JsonArray)line["segments"]!;
            for (var i = segments.Count - 1; i >= 0; i--)
            {
                if (Py.Eq(segments[i]!["segmentRef"], segRef))
                    segments.RemoveAt(i);
            }
        }
        _state.Segments.Remove(S(segRef));
        BumpRevision();
        return OkTrue();
    }

    // -- Phase G: Bus interface / filter handlers ---------------------------------------------

    private JsonNode? HandleBusInterfaceLink(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var gaRefs = Get(p, "groupAddressRefs");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(gaRefs))
            throw Fail("invalid_params", "deviceRef and groupAddressRefs are required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        var refs = Py.Iter(gaRefs);
        foreach (var gaRef in refs)
        {
            if (!Has(_state.GroupAddresses, gaRef))
                throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
        }
        var links = SetDefaultList(_state.BusInterfaceLinks, S(deviceRef));
        foreach (var gaRef in refs)
        {
            if (!Py.ListContains(links, gaRef))
                Push(links, Clone(gaRef));
        }
        BumpRevision();
        return OkTrue();
    }

    private JsonNode? HandleBusInterfaceUnlink(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var gaRefs = Get(p, "groupAddressRefs");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(gaRefs))
            throw Fail("invalid_params", "deviceRef and groupAddressRefs are required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        var links = Find(_state.BusInterfaceLinks, deviceRef) ?? new JsonArray();
        foreach (var gaRef in Py.Iter(gaRefs))
        {
            var index = Py.IndexOf(links, gaRef);
            if (index >= 0)
                links.RemoveAt(index);
        }
        BumpRevision();
        return OkTrue();
    }
}
