using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    private static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>Python <c>_seed_data</c>: the fixed seed project (refs, values and insertion order).</summary>
    private void SeedData()
    {
        _state.Project = J("""
            {"projectId": "proj-001", "name": "Test KNX Project", "groupAddressStyle": "ThreeLevel"}
            """);

        // Device 1: Switch Actuator
        var co11 = J("""
            {"ref": "co-0001", "deviceRef": "dev-0001", "number": 0, "name": "Switch Object A",
             "description": "", "functionText": "Raffstore links", "text": "Schalten Ausgang A",
             "dpt": "1.001", "isActive": true,
             "flags": {"read": true, "write": true, "transmit": false, "update": false}}
            """);
        var co12 = J("""
            {"ref": "co-0002", "deviceRef": "dev-0001", "number": 1, "name": "Switch Object B",
             "description": "Status feedback", "functionText": "", "text": "",
             "dpt": "1.001", "isActive": true,
             "flags": {"read": true, "write": true, "transmit": false, "update": false}}
            """);
        // Inactive -- linking must fail
        var co13 = J("""
            {"ref": "co-0003", "deviceRef": "dev-0001", "number": 2, "name": "Status Object",
             "description": "", "functionText": "", "text": "",
             "dpt": "1.001", "isActive": false,
             "flags": {"read": true, "write": false, "transmit": true, "update": false}}
            """);
        _state.Devices["dev-0001"] = J("""
            {"ref": "dev-0001", "address": "1.1.1", "name": "Switch Actuator 4x",
             "description": "Couch", "comment": "Main actuator for living room",
             "product": "ABB SA/S 4.16.1", "orderNumber": "SA/S 4.16.1", "line": "line-1.1"}
            """);
        foreach (var co in new[] { co11, co12, co13 })
            _state.ComObjects[S(co["ref"])] = co;

        // Enumerated parameter: the LLM sees the allowed choices + labels.
        var param11 = J("""
            {"ref": "param-0001", "deviceRef": "dev-0001", "name": "Operating Mode",
             "value": "normal", "defaultValue": "normal", "isActive": true,
             "text": "Operating Mode", "access": "ReadWrite",
             "options": [
                {"value": "normal", "text": "Normal"},
                {"value": "night", "text": "Night"},
                {"value": "comfort", "text": "Comfort"}
             ]}
            """);
        // Inactive: hidden until "Operating Mode" is changed -- demonstrates that a FALSE
        // isActive means "controlled by another parameter", not "set me".
        var param12 = J("""
            {"ref": "param-0003", "deviceRef": "dev-0001", "name": "Switch-on delay",
             "value": "0", "defaultValue": "0", "isActive": false,
             "text": "Switch-on delay", "unit": "s", "access": "ReadWrite",
             "min": "0", "max": "255",
             "block": "Operation / Display > Push button functions > PB9/10: Push buttons 9/10"}
            """);
        _state.Parameters["param-0001"] = param11;
        _state.Parameters["param-0003"] = param12;

        // Device 2: Dimming Actuator
        var co21 = J("""
            {"ref": "co-0004", "deviceRef": "dev-0002", "number": 0, "name": "Dim Object A",
             "description": "", "functionText": "", "text": "",
             "dpt": "5.001", "isActive": true,
             "channel": "Channel A - Dimming",
             "block": "Operation / Display > Dimming > Channel A",
             "flags": {"read": true, "write": true, "transmit": false, "update": false}}
            """);
        var co22 = J("""
            {"ref": "co-0005", "deviceRef": "dev-0002", "number": 1, "name": "Dim Object B",
             "description": "", "functionText": "", "text": "",
             "dpt": "5.001", "isActive": true,
             "channel": "Channel B - Dimming",
             "flags": {"read": true, "write": true, "transmit": false, "update": false}}
            """);
        _state.Devices["dev-0002"] = J("""
            {"ref": "dev-0002", "address": "1.1.2", "name": "Dimming Actuator 2x",
             "description": "", "comment": "",
             "product": "ABB DA/S 2.1", "orderNumber": "DA/S 2.1", "line": "line-1.1"}
            """);
        foreach (var co in new[] { co21, co22 })
            _state.ComObjects[S(co["ref"])] = co;

        _state.Parameters["param-0002"] = J("""
            {"ref": "param-0002", "deviceRef": "dev-0002", "name": "Dimming Speed",
             "value": "3", "defaultValue": "5", "isActive": true}
            """);

        // Group addresses (addresses as strings like "1/0/3"). ga-0002 has no description key.
        _state.GroupAddresses["ga-0001"] = J("""
            {"ref": "ga-0001", "address": "1/0/1", "name": "Light Living Room", "dpt": "1.001",
             "description": "Mittelgang", "comment": "Ceiling light"}
            """);
        _state.GroupAddresses["ga-0002"] = J("""
            {"ref": "ga-0002", "address": "1/0/2", "name": "Dimmer Living Room", "dpt": "5.001",
             "comment": ""}
            """);

        // One pre-existing link
        _links.Add(("co-0001", "ga-0001"));

        // Topology: 1 area, 2 lines, line-1.1 has a segment
        _state.Topology = new JsonArray(J("""
            {"areaRef": "area-1", "address": "1", "name": "Building A",
             "description": "Main building area", "comment": "",
             "lines": [
                {"lineRef": "line-1.1", "address": "1.1", "name": "Main Line",
                 "description": "Primary TP line", "comment": "",
                 "segments": [
                    {"segmentRef": "seg-1.1.0", "name": "Segment 0", "description": "", "comment": ""}
                 ]},
                {"lineRef": "line-1.2", "address": "1.2", "name": "Secondary Line",
                 "description": "", "comment": "", "segments": []}
             ]}
            """));

        // Catalog manufacturers
        _manufacturers["mfr-abb"] = J("""{"manufacturerRef": "mfr-abb", "name": "ABB"}""");
        _manufacturers["mfr-gira"] = J("""{"manufacturerRef": "mfr-gira", "name": "Gira"}""");

        // Catalog items (local product store)
        _catalogItems["cat-abb-sa4"] = J("""
            {"catalogItemRef": "cat-abb-sa4", "manufacturer": "ABB", "manufacturerRef": "mfr-abb",
             "name": "Switch Actuator 4-fold", "orderNumber": "SA/S 4.16.1",
             "description": "4-channel switch actuator 16A"}
            """);
        _catalogItems["cat-abb-da2"] = J("""
            {"catalogItemRef": "cat-abb-da2", "manufacturer": "ABB", "manufacturerRef": "mfr-abb",
             "name": "Dimming Actuator 2-fold", "orderNumber": "DA/S 2.1",
             "description": "2-channel universal dimming actuator"}
            """);
        _catalogItems["cat-gira-ts3"] = J("""
            {"catalogItemRef": "cat-gira-ts3", "manufacturer": "Gira", "manufacturerRef": "mfr-gira",
             "name": "Tastsensor 3 Komfort", "orderNumber": "2031 00",
             "description": "3-fold push button sensor"}
            """);

        // Online (unified) catalog -- curated by KNX, NOT in local store.
        // Items here use "uci:" refs and are only usable after internalize.
        _unifiedCatalogItems["uci:abb-spau"] = J("""
            {"unifiedCatalogItemRef": "uci:abb-spau", "manufacturer": "ABB", "manufacturerRef": "mfr-abb",
             "name": "Space Unit Sensor", "orderNumber": "SPAU/F 1.1"}
            """);
        _unifiedCatalogItems["uci:gira-be2"] = J("""
            {"unifiedCatalogItemRef": "uci:gira-be2", "manufacturer": "Gira", "manufacturerRef": "mfr-gira",
             "name": "Binary Input 2-fold", "orderNumber": "1120 00"}
            """);
        _unifiedCatalogItems["uci:abb-rog"] = J("""
            {"unifiedCatalogItemRef": "uci:abb-rog", "manufacturer": "ABB", "manufacturerRef": "mfr-abb",
             "name": "Room Controller", "orderNumber": "ROG/A 1.1"}
            """);

        // Importable .knxprod paths -> items they produce
        _importablePaths["C:\\Products\\sensor.knxprod"] = new JsonArray(J("""
            {"catalogItemRef": "cat-imp-sensor", "manufacturer": "ABB", "manufacturerRef": "mfr-abb",
             "name": "Imported Sensor Module", "orderNumber": "SM/S 1.1"}
            """));

        // Group ranges (mirrors the 3-level structure: main -> middle)
        _state.GroupRanges["gr-main-1"] = J("""
            {"ref": "gr-main-1", "name": "Lighting", "address": 1, "parentRef": null,
             "description": "All lighting GAs", "comment": ""}
            """);
        _state.GroupRanges["gr-mid-1-0"] = J("""
            {"ref": "gr-mid-1-0", "name": "Living Room", "address": 0, "parentRef": "gr-main-1",
             "description": "", "comment": ""}
            """);

        // Building structure: Building -> Floor -> Room (with device + function)
        _state.BuildingParts["bp-bld-1"] = J("""
            {"ref": "bp-bld-1", "name": "Main Building", "type": "Building", "parentRef": null,
             "devices": [], "description": "Primary building", "comment": ""}
            """);
        _state.BuildingParts["bp-floor-1"] = J("""
            {"ref": "bp-floor-1", "name": "Ground Floor", "type": "Floor", "parentRef": "bp-bld-1",
             "devices": [], "description": "", "comment": ""}
            """);
        _state.BuildingParts["bp-room-1"] = J("""
            {"ref": "bp-room-1", "name": "Living Room", "type": "Room", "parentRef": "bp-floor-1",
             "devices": ["dev-0001"], "description": "", "comment": "Main living area"}
            """);

        _state.BuildingFunctions["bf-0001"] = J("""
            {"ref": "bf-0001", "name": "Lighting Control", "buildingPartRef": "bp-room-1",
             "groupAddresses": ["ga-0001"], "description": "Central lighting function", "comment": ""}
            """);

        // Trades
        _state.TradeCounter = 1;
        _state.Trades["trade-0001"] = J("""
            {"ref": "trade-0001", "name": "HVAC", "number": 1, "devices": ["dev-0001"],
             "description": "Heating, ventilation, air conditioning", "comment": ""}
            """);

        // Tags (ID-based refs: tag:{Id})
        _state.TagCounter = 1;
        _state.Tags["tag:t1"] = J("""{"ref": "tag:t1", "label": "Important", "color": "#FF0000"}""");

        // ToDo items (ID-based refs: todo:{Id}; canonical status: Open / Accomplished)
        _state.TodoCounter = 1;
        _state.Todos["todo:t1"] = J("""
            {"ref": "todo:t1", "description": "Check wiring",
             "objectPath": "Building A/Ground Floor", "status": "Open"}
            """);

        // Certificates (no secrets; deviceRef instead of hasDevice)
        _state.Certificates["cert-0001"] = J("""
            {"ref": "cert-0001", "serialNumber": "00FA12345678ABCD", "deviceRef": "dev-0001"}
            """);

        // Additional device addresses
        _state.AdditionalDeviceAddresses["dev-0001"] = new JsonArray(JsonValue.Create(10));

        // Project history (ID-based refs: ph:{Id})
        _state.HistoryCounter = 1;
        _state.ProjectHistory["ph:h1"] = J("""
            {"ref": "ph:h1", "date": "2025-01-15T10:00:00Z", "text": "Project created",
             "detail": "", "user": "admin"}
            """);

        // Index the seed segment so segment.delete can find it
        _state.Segments["seg-1.1.0"] = J("""
            {"segmentRef": "seg-1.1.0", "name": "Segment 0", "lineRef": "line-1.1",
             "mediumType": "TP", "description": "", "comment": ""}
            """);
    }
}
