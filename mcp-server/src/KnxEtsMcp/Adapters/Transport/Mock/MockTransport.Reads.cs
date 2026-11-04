using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    // -- Read handlers ---------------------------------------------------------------

    private JsonNode? HandleBridgeInfo(JsonNode? _) => new JsonObject
    {
        ["addinVersion"] = "0.4.1",
        ["sdkVersion"] = "6.3.7959.0",       // runtime-loaded (host ETS) SDK
        ["builtAgainstSdk"] = "6.4.8658.0",  // compile-time SDK (default build target)
        ["projectName"] = Clone(_state.Project["name"]),
        ["projectId"] = Clone(_state.Project["projectId"]),
        ["projectRevision"] = _state.Revision,
        ["knxIpOnly"] = true,
    };

    private JsonNode? HandleProjectInfo(JsonNode? _)
    {
        var info = (JsonObject)_state.Project.DeepClone();
        info["revision"] = _state.Revision;
        return info;
    }

    private JsonNode? HandleDevicesList(JsonNode? _) => CopyList(_state.Devices.Values);

    private JsonNode? HandleGaList(JsonNode? _) => CopyList(_state.GroupAddresses.Values);

    private JsonNode? HandleComObjectsList(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        var result = new JsonArray();
        foreach (var co in _state.ComObjects.Values)
        {
            if (!Py.Eq(co["deviceRef"], deviceRef))
                continue;
            var coRef = S(co["ref"]);
            var links = new JsonArray();
            foreach (var (linkCo, linkGa) in _links)
            {
                if (linkCo == coRef)
                    Push(links, linkGa);
            }
            var entry = new JsonObject
            {
                ["ref"] = Clone(co["ref"]),
                ["number"] = Clone(co["number"]),
                ["name"] = Clone(co["name"]),
                ["description"] = Clone(GetOr(co, "description", "")),
                ["functionText"] = Clone(GetOr(co, "functionText", "")),
                ["text"] = Clone(GetOr(co, "text", "")),
                ["dpt"] = Clone(co["dpt"]),
                ["flags"] = Clone(co["flags"]),
                ["links"] = links,
            };
            // Channel/function grouping label, omitted when channel-independent (mirrors
            // the gateway which omits an empty channel).
            if (Py.Truthy(co["channel"]))
                entry["channel"] = Clone(co["channel"]);
            // Authoritative ETS group-object-tree block path (mirrors gateway BuildBlockPath).
            if (Py.Truthy(co["block"]))
                entry["block"] = Clone(co["block"]);
            Push(result, entry);
        }
        return result;
    }

    private JsonNode? HandleTopologyList(JsonNode? _) => _state.Topology.DeepClone();

    private JsonNode? HandleCatalogManufacturers(JsonNode? _) => CopyList(_manufacturers.Values);

    private JsonNode? HandleCatalogSearch(JsonNode? p)
    {
        var query = Get(p, "query");
        if (!Py.Truthy(query))
            throw Fail("invalid_params", "query is required");

        var manufacturerRef = Get(p, "manufacturerRef");
        if (Py.Truthy(manufacturerRef) && !Has(_manufacturers, manufacturerRef))
            throw Fail("not_found", $"Manufacturer {Py.Str(manufacturerRef)} not found");

        var queryLower = Py.RequireStr(query, "lower").ToLowerInvariant();
        var results = new JsonArray();
        foreach (var item in _catalogItems.Values)
        {
            if (Py.Truthy(manufacturerRef) && !Py.Eq(item["manufacturerRef"], manufacturerRef))
                continue;
            if (!S(item["name"]).ToLowerInvariant().Contains(queryLower, StringComparison.Ordinal)
                && !S(item["orderNumber"]).ToLowerInvariant().Contains(queryLower, StringComparison.Ordinal))
            {
                continue;
            }
            Push(results, new JsonObject
            {
                ["catalogItemRef"] = Clone(item["catalogItemRef"]),
                ["manufacturer"] = Clone(item["manufacturer"]),
                ["name"] = Clone(item["name"]),
                ["orderNumber"] = Clone(item["orderNumber"]),
                ["description"] = Clone(GetOr(item, "description", "")),
            });
        }
        return results;
    }

    private JsonNode? HandleCatalogSearchOnline(JsonNode? p)
    {
        var query = Get(p, "query");
        if (!Py.Truthy(query))
            throw Fail("invalid_params", "query is required");

        var manufacturerRef = Get(p, "manufacturerRef");
        if (Py.Truthy(manufacturerRef) && !Has(_manufacturers, manufacturerRef))
            throw Fail("not_found", $"Manufacturer {Py.Str(manufacturerRef)} not found");

        var queryLower = Py.RequireStr(query, "lower").ToLowerInvariant();
        var results = new JsonArray();
        foreach (var item in _unifiedCatalogItems.Values)
        {
            if (Py.Truthy(manufacturerRef) && !Py.Eq(item["manufacturerRef"], manufacturerRef))
                continue;
            if (!S(item["name"]).ToLowerInvariant().Contains(queryLower, StringComparison.Ordinal)
                && !S(item["orderNumber"]).ToLowerInvariant().Contains(queryLower, StringComparison.Ordinal))
            {
                continue;
            }
            Push(results, new JsonObject
            {
                ["unifiedCatalogItemRef"] = Clone(item["unifiedCatalogItemRef"]),
                ["manufacturer"] = Clone(item["manufacturer"]),
                ["name"] = Clone(item["name"]),
                ["orderNumber"] = Clone(item["orderNumber"]),
            });
        }
        return results;
    }

    private JsonNode? HandleCatalogImport(JsonNode? p)
    {
        var path = Get(p, "path");
        if (!Py.Truthy(path))
            throw Fail("invalid_params", "path is required");

        var items = Find(_importablePaths, path)
            ?? throw Fail("not_found", $"File not found or invalid: {Py.Str(path)}");

        var imported = new JsonArray();
        foreach (var node in items)
        {
            var item = (JsonObject)node!;
            var itemRef = S(item["catalogItemRef"]);
            if (!_catalogItems.ContainsKey(itemRef))
                _catalogItems[itemRef] = (JsonObject)item.DeepClone();
            Push(imported, new JsonObject
            {
                ["catalogItemRef"] = Clone(item["catalogItemRef"]),
                ["manufacturer"] = Clone(item["manufacturer"]),
                ["name"] = Clone(item["name"]),
                ["orderNumber"] = Clone(item["orderNumber"]),
            });
        }
        return new JsonObject { ["imported"] = imported };
    }

    private JsonNode? HandleCatalogInternalize(JsonNode? p)
    {
        var uciRef = Get(p, "unifiedCatalogItemRef");
        if (!Py.Truthy(uciRef))
            throw Fail("invalid_params", "unifiedCatalogItemRef is required");

        var unified = Find(_unifiedCatalogItems, uciRef)
            ?? throw Fail("not_found", $"Unified catalog item {Py.Str(uciRef)} not found");

        // Derive a local catalogItemRef from the unified ref.
        var uci = S(uciRef);
        var localRef = "ci:" + (uci.StartsWith("uci:", StringComparison.Ordinal) ? uci["uci:".Length..] : uci);
        if (!_catalogItems.ContainsKey(localRef))
        {
            _catalogItems[localRef] = new JsonObject
            {
                ["catalogItemRef"] = localRef,
                ["manufacturer"] = Clone(unified["manufacturer"]),
                ["manufacturerRef"] = Clone(unified["manufacturerRef"]),
                ["name"] = Clone(unified["name"]),
                ["orderNumber"] = Clone(unified["orderNumber"]),
            };
        }
        return new JsonObject { ["catalogItemRef"] = localRef };
    }

    private static readonly string[] ParamEnrichmentKeys = ["text", "unit", "access", "options", "min", "max", "block"];

    private JsonNode? HandleParamsList(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        var results = new JsonArray();
        foreach (var param in _state.Parameters.Values)
        {
            if (!Py.Eq(param["deviceRef"], deviceRef))
                continue;
            var entry = new JsonObject
            {
                ["parameterRef"] = Clone(param["ref"]),
                ["name"] = Clone(param["name"]),
                ["value"] = Clone(param["value"]),
                ["isDefault"] = Py.Eq(param["value"], param["defaultValue"]),
                ["isActive"] = Clone(param["isActive"]),
            };
            // Optional product-data semantics -- emitted only when present (Python:
            // `p.get(key) not in (None, "", [])`), mirroring the C# gateway which omits
            // empty/null enrichment fields from the JSON.
            foreach (var key in ParamEnrichmentKeys)
            {
                var value = param[key];
                if (Py.IsNone(value) || Py.Eq(value, "") || Py.Eq(value, new JsonArray()))
                    continue;
                entry[key] = Clone(value);
            }
            Push(results, entry);
        }
        return results;
    }

    // -- Phase E: Catalog browsing ------------------------------------------------------

    private JsonNode? HandleCatalogBrowseProducts(JsonNode? p)
    {
        var manufacturerRef = Get(p, "manufacturerRef");
        if (!Py.Truthy(manufacturerRef))
            throw Fail("invalid_params", "manufacturerRef is required");
        if (!Has(_manufacturers, manufacturerRef))
            throw Fail("not_found", $"Manufacturer {Py.Str(manufacturerRef)} not found");

        var offsetNode = GetOr(p, "offset", 0);
        var limitNode = GetOr(p, "limit", 100);

        var items = new List<JsonObject>();
        foreach (var item in _catalogItems.Values)
        {
            if (!Py.Eq(GetOr(item, "manufacturerRef", null), manufacturerRef))
                continue;
            items.Add(new JsonObject
            {
                ["catalogItemRef"] = Clone(item["catalogItemRef"]),
                ["manufacturer"] = Clone(item["manufacturer"]),
                ["name"] = Clone(item["name"]),
                ["orderNumber"] = Clone(item["orderNumber"]),
                ["description"] = Clone(GetOr(item, "description", "")),
            });
        }

        // Python items[offset:offset + limit] (negative indices count from the end).
        var offset = Py.Index(offsetNode);
        var stop = offset + Py.Index(limitNode);
        var start = ClampSliceIndex(offset, items.Count);
        var end = ClampSliceIndex(stop, items.Count);
        var page = new JsonArray();
        for (var i = start; i < end; i++)
            Push(page, items[i]);
        return page;
    }

    private static int ClampSliceIndex(long index, int length)
    {
        if (index < 0)
            index += length;
        return (int)Math.Clamp(index, 0, length);
    }

    private JsonNode? HandleCatalogProductInfo(JsonNode? p)
    {
        var catRef = Get(p, "catalogItemRef");
        if (!Py.Truthy(catRef))
            throw Fail("invalid_params", "catalogItemRef is required");
        var item = Find(_catalogItems, catRef)
            ?? throw Fail("not_found", $"CatalogItem {Py.Str(catRef)} not found");

        return new JsonObject
        {
            ["catalogItemRef"] = Clone(item["catalogItemRef"]),
            ["manufacturer"] = Clone(item["manufacturer"]),
            ["name"] = Clone(item["name"]),
            ["orderNumber"] = Clone(item["orderNumber"]),
            ["description"] = Clone(GetOr(item, "description", "")),
            ["mediumTypes"] = Clone(GetOr(item, "mediumTypes", new JsonArray())),
        };
    }

    // -- Phase G: Channels / modules ------------------------------------------------------

    /// <summary>application.dynamic -> {xml}: the app-program dynamic UI tree.</summary>
    private JsonNode? HandleApplicationDynamic(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        // A small representative dynamic tree (real devices return a large XML string).
        const string xml =
            "<Dynamic>"
            + "<ChannelIndependentBlock/>"
            + "<Channel Name=\"CH-A\" Text=\"Channel A - Dimming\">"
            + "<ParameterBlock Name=\"PB-A\" Text=\"Dimming\">"
            + "<ParameterRefRef RefId=\"param-0002\"/>"
            + "</ParameterBlock></Channel>"
            + "</Dynamic>";
        return new JsonObject { ["xml"] = xml };
    }

    /// <summary>device.channels -- mirrors C# DeviceChannelsResult / ChannelInstanceInfo.</summary>
    private JsonNode? HandleDeviceChannels(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        // Build channels from com-objects belonging to this device
        var channels = new JsonArray();
        foreach (var co in _state.ComObjects.Values)
        {
            if (!Py.Eq(co["deviceRef"], deviceRef))
                continue;
            var isActive = GetOr(co, "isActive", true);
            var activeRefs = new JsonArray();
            if (Py.Truthy(isActive))
                Push(activeRefs, Clone(co["ref"]));
            Push(channels, new JsonObject
            {
                ["name"] = $"Channel {Py.Str(co["number"])}",
                ["text"] = Clone(GetOr(co, "text", "")),
                ["description"] = null,
                ["isActive"] = Clone(isActive),
                ["applicationProgramChannelId"] = null,
                ["activeComObjectRefs"] = activeRefs,
            });
        }
        return new JsonObject
        {
            ["deviceRef"] = Clone(deviceRef),
            ["channels"] = channels,
            ["modules"] = new JsonArray(),
        };
    }
}
