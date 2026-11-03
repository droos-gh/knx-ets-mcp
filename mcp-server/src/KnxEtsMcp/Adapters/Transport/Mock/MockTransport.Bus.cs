using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    private static readonly HashSet<string> ValidProgramOptions = new(StringComparer.Ordinal)
    {
        "all", "partial", "application", "network", "networkBySerial",
    };

    /// <summary>Registers an already-finished job (the mock completes every job immediately).</summary>
    private string AddDoneJob(JsonNode? result)
    {
        var jobId = NewJobId();
        _jobs[jobId] = new JsonObject
        {
            ["state"] = "done",
            ["percent"] = 100,
            ["error"] = null,
            ["result"] = result,
        };
        return jobId;
    }

    private static JsonObject JobIdResult(string jobId) => new() { ["jobId"] = jobId };

    // -- Programming / jobs -------------------------------------------------------------

    private JsonNode? HandleDeviceProgram(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        // Validate options if provided (None or one of the known option names)
        var options = Get(p, "options");
        if (!Py.IsNone(options)
            && (Py.HashKey(options) is not { } option || !ValidProgramOptions.Contains(option)))
        {
            throw Fail("invalid_params", $"Unknown option: '{Py.Str(options)}'");
        }

        return JobIdResult(AddDoneJob(null));
    }

    private JsonNode? HandleJobStatus(JsonNode? p)
    {
        var jobId = Get(p, "jobId");
        if (!Py.Truthy(jobId))
            throw Fail("invalid_params", "jobId is required");
        var job = Find(_jobs, jobId)
            ?? throw Fail("not_found", $"Job {Py.Str(jobId)} not found");
        var result = new JsonObject
        {
            ["state"] = Clone(job["state"]),
            ["percent"] = Clone(job["percent"]),
        };
        if (Py.Truthy(job["error"]))
            result["error"] = Clone(job["error"]);
        if (!Py.IsNone(job["result"]))
            result["result"] = Clone(job["result"]);
        return result;
    }

    private JsonNode? HandleJobCancel(JsonNode? p)
    {
        var jobId = Get(p, "jobId");
        if (!Py.Truthy(jobId))
            throw Fail("invalid_params", "jobId is required");
        var job = Find(_jobs, jobId)
            ?? throw Fail("not_found", $"Job {Py.Str(jobId)} not found");
        // Idempotent: already terminal -> no-op
        var state = job["state"];
        if (!Py.Eq(state, "done") && !Py.Eq(state, "failed") && !Py.Eq(state, "canceled"))
            job["state"] = "canceled";
        return OkTrue();
    }

    /// <summary>
    /// firmware.update -- gated by the AddIn's "Unattended firmware update" preference. The
    /// mock simulates the default state (preference disabled) and always returns
    /// approval_required. There is no per-request approval token.
    /// </summary>
    private JsonNode? HandleFirmwareUpdate(JsonNode? _)
        => throw Fail("approval_required",
            "The AddIn's 'Unattended firmware update' preference is not enabled");

    // -- Phase D: Bus / Online operation handlers ------------------------------------------

    private JsonNode? HandleBusPing(JsonNode? p)
    {
        var address = Get(p, "address");
        if (!Py.Truthy(address))
            throw Fail("invalid_params", "address is required");
        // Normalise and validate format
        var normalized = NormalizeIndividualAddress(address);
        // Mock: addresses ending in .0 are "not alive", others are alive
        var alive = !normalized.EndsWith(".0", StringComparison.Ordinal);
        return new JsonObject { ["alive"] = alive };
    }

    private JsonNode? HandleBusScanLine(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        if (!Py.Truthy(lineRef))
            throw Fail("invalid_params", "lineRef is required");
        // Validate line exists
        if (!AllLines().Any(line => Py.Eq(line["lineRef"], lineRef)))
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");

        // Mock: immediately complete the job with known device addresses on the line
        var jobId = NewJobId();
        var addresses = new JsonArray();
        foreach (var device in _state.Devices.Values)
        {
            if (Py.Eq(GetOr(device, "line", null), lineRef))
                Push(addresses, Clone(device["address"]));
        }
        _jobs[jobId] = new JsonObject
        {
            ["state"] = "done",
            ["percent"] = 100,
            ["error"] = null,
            ["result"] = new JsonObject { ["addresses"] = addresses },
        };
        return JobIdResult(jobId);
    }

    private JsonNode? HandleBusReconstructLine(JsonNode? p)
    {
        var lineRef = Get(p, "lineRef");
        if (!Py.Truthy(lineRef))
            throw Fail("invalid_params", "lineRef is required");
        // Validate line exists and resolve its address prefix
        JsonNode? lineAddress = null;
        foreach (var line in AllLines())
        {
            if (Py.Eq(line["lineRef"], lineRef))
            {
                lineAddress = line["address"];
                break;
            }
        }
        if (Py.IsNone(lineAddress))
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");

        // Build device list from known devices on this line
        var devices = new JsonArray();
        foreach (var device in _state.Devices.Values)
        {
            if (!Py.Eq(GetOr(device, "line", null), lineRef))
                continue;
            Push(devices, new JsonObject
            {
                ["address"] = Clone(device["address"]),
                ["maskVersion"] = "0705",
                ["serialNumber"] = "00FA12345678",
                ["manufacturerId"] = 2,
                ["error"] = null,
            });
        }

        var prefix = Py.Str(lineAddress);
        var jobId = AddDoneJob(new JsonObject
        {
            ["devices"] = devices,
            ["scannedRange"] = $"{prefix}.1-{prefix}.255",
        });
        return JobIdResult(jobId);
    }

    private JsonNode? HandleDeviceReadInfo(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        // Mock: return a fake mask version
        return new JsonObject { ["maskVersion"] = "0x0705", ["maskVersionId"] = "MV-0705" };
    }

    /// <summary>
    /// device.compare -- limited compare. Returns a small sample comparison list; a full
    /// byte-level compare is not feasible via the SDK's high-level API, so partial=true.
    /// </summary>
    private JsonNode? HandleDeviceCompare(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        var device = Find(_state.Devices, deviceRef)
            ?? throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");

        // Mock: return a small sample comparison
        return new JsonObject
        {
            ["compared"] = new JsonArray(
                new JsonObject
                {
                    ["property"] = "individualAddress",
                    ["equal"] = true,
                    ["expected"] = Clone(device["address"]),
                    ["actual"] = Clone(device["address"]),
                },
                new JsonObject
                {
                    ["property"] = "applicationLoaded",
                    ["equal"] = false,
                    ["expected"] = true,
                    ["actual"] = false,
                }),
            ["partial"] = true,
            ["note"] = "Limited compare via SDK high-level API; full byte-level compare not available.",
        };
    }

    private JsonNode? HandleDeviceReadGroupObjects(JsonNode? p)
    {
        var addressNode = Get(p, "address");
        if (!Py.Truthy(addressNode))
            throw Fail("invalid_params", "address is required");

        // Normalise address (tolerates commas/whitespace)
        var address = NormalizeIndividualAddress(addressNode);

        // Find device by its individual address
        var device = _state.Devices.Values.FirstOrDefault(dev => Py.Eq(dev["address"], address))
            ?? throw Fail("not_found", $"No device at address {address}");

        // Build the result payload (shared by sync and async modes)
        var result = BuildGroupObjectsResult(device, address);

        // Dual-mode: async=true returns a jobId
        if (Py.Truthy(Get(p, "async")))
            return JobIdResult(AddDoneJob(result));

        // Default (sync): return the result directly
        return result;
    }

    /// <summary>Build the DeviceGroupObjectsResult payload for a device.</summary>
    private JsonObject BuildGroupObjectsResult(JsonObject device, string address)
    {
        var deviceRef = device["ref"];
        var comObjects = new JsonArray();
        foreach (var co in _state.ComObjects.Values)
        {
            if (!Py.Eq(co["deviceRef"], deviceRef))
                continue;
            var coRef = S(co["ref"]);
            var gaAddresses = new JsonArray();
            foreach (var (linkCo, linkGa) in _links)
            {
                if (linkCo != coRef)
                    continue;
                // Python `if ga:` -- a missing GA (or an empty dict) is skipped.
                if (_state.GroupAddresses.TryGetValue(linkGa, out var ga) && ga.Count > 0)
                    Push(gaAddresses, Clone(ga["address"]));
            }
            var flags = GetOr(co, "flags", new JsonObject());
            Push(comObjects, new JsonObject
            {
                ["number"] = Clone(co["number"]),
                ["groupAddresses"] = gaAddresses,
                ["flags"] = new JsonObject
                {
                    ["c"] = Clone(GetOr(flags, "communication", true)),
                    ["r"] = Clone(GetOr(flags, "read", false)),
                    ["w"] = Clone(GetOr(flags, "write", false)),
                    ["t"] = Clone(GetOr(flags, "transmit", false)),
                    ["u"] = Clone(GetOr(flags, "update", false)),
                },
            });
        }

        return new JsonObject
        {
            ["address"] = address,
            ["comObjects"] = comObjects,
            ["partial"] = true,
            ["note"] = "KNX Secure objects may not be readable without the correct key.",
        };
    }

    private JsonNode? HandleGroupRead(JsonNode? p)
    {
        var gaRef = Get(p, "gaRef");
        if (!Py.Truthy(gaRef))
            throw Fail("invalid_params", "gaRef is required");
        var ga = Find(_state.GroupAddresses, gaRef)
            ?? throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
        // Mock: simulated value (hex). DPT "1.x" -> boolean "01", else "64" (100 decimal).
        // A GA created without DPT stores None, on which Python's startswith crashes.
        var dpt = Py.RequireStr(GetOr(ga, "dpt", ""), "startswith");
        if (dpt.StartsWith("1.", StringComparison.Ordinal))
            return new JsonObject { ["value"] = "01" };
        return new JsonObject { ["value"] = "64" };
    }

    private JsonNode? HandleGroupWrite(JsonNode? p)
    {
        var gaRef = Get(p, "gaRef");
        var value = Get(p, "value");
        if (!Py.Truthy(gaRef) || Py.IsNone(value))
            throw Fail("invalid_params", "gaRef and value are required");
        if (!Has(_state.GroupAddresses, gaRef))
            throw Fail("not_found", $"GroupAddress {Py.Str(gaRef)} not found");
        // Validate hex (bytes.fromhex raises TypeError -- a crash -- for a non-str)
        var hex = Py.RequireStr(value, "fromhex");
        if (!Py.IsValidFromHex(hex))
            throw Fail("invalid_params", $"Invalid hex value: '{hex}'");
        // Mock: always acknowledge
        return new JsonObject { ["acknowledged"] = true };
    }

    private JsonNode? HandleGroupMonitor(JsonNode? p)
    {
        var lineRef = GetOr(p, "lineRef", "default");
        var durationMs = Py.Real(GetOr(p, "durationMs", 10000));
        if (durationMs < 1000 || durationMs > 300000)
            throw Fail("invalid_params", "durationMs must be between 1000 and 300000");

        // Validate line exists (unless "default")
        if (!Py.Eq(lineRef, "default")
            && !AllLines().Any(line => Py.Eq(line["lineRef"], lineRef)))
        {
            throw Fail("not_found", $"Line {Py.Str(lineRef)} not found");
        }

        // Mock: immediately complete with a few fake telegrams
        var jobId = AddDoneJob(new JsonObject
        {
            ["telegrams"] = new JsonArray(
                new JsonObject
                {
                    ["service"] = "GroupValueWrite",
                    ["sourceAddress"] = "1.1.1",
                    ["groupAddress"] = "1/0/1",
                    ["value"] = "01",
                    ["timestamp"] = "2025-01-01T00:00:00.000Z",
                },
                new JsonObject
                {
                    ["service"] = "GroupValueResponse",
                    ["sourceAddress"] = "1.1.2",
                    ["groupAddress"] = "1/0/2",
                    ["value"] = "64",
                    ["timestamp"] = "2025-01-01T00:00:01.000Z",
                }),
        });
        return JobIdResult(jobId);
    }

    private JsonNode? HandleDeviceUnload(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        return JobIdResult(AddDoneJob(null));
    }

    private JsonNode? HandleBusSetIndividualAddress(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        var currentAddress = Get(p, "currentAddress");
        if (!Py.Truthy(deviceRef) || !Py.Truthy(currentAddress))
            throw Fail("invalid_params", "deviceRef and currentAddress are required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        return JobIdResult(AddDoneJob(null));
    }

    // -- Phase G: Bus write handlers (NOT mutating) --------------------------------------------

    private JsonNode? HandleDeviceReset(JsonNode? p)
    {
        var deviceRef = Get(p, "deviceRef");
        if (!Py.Truthy(deviceRef))
            throw Fail("invalid_params", "deviceRef is required");
        if (!Has(_state.Devices, deviceRef))
            throw Fail("not_found", $"Device {Py.Str(deviceRef)} not found");
        return OkTrue();
    }
}
