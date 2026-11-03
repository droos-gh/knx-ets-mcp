// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Read tools.</summary>
[McpServerToolType]
public sealed class ReadTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_bridge_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Get bridge/AddIn status: version, SDK version, active project metadata, KNXnet/IP constraint.
        """)]
    public async Task<JsonNode?> BridgeInfo(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("bridge.info", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_project_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Get KNX project metadata: ID, name, group-address style, revision.
        """)]
    public async Task<JsonNode?> ProjectInfo(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("project.info", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_list_devices", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List devices in the KNX project (ref, address, name, description, comment, product,
        orderNumber, line).

        Finding a device by a HUMAN label (e.g. "Couch"): the `name` field is usually the
        catalog PRODUCT name (e.g. "ABB BE-GT2Tx.01 ..."), not the label you gave it. The
        label typically lives in `description`/`comment` (or the room it is assigned to -- see
        knx_list_building). So use `name_contains`: it matches case-insensitively across
        name/description/comment/product/orderNumber/address. If a label is not found this way,
        it may be a room/building assignment (knx_list_building) rather than a device field.

        Args:
            name_contains: Optional case-insensitive substring filter across
                name/description/comment/product/orderNumber/address.
        """)]
    public async Task<JsonNode?> ListDevices(
        [Description("Optional case-insensitive substring filter across name/description/comment/product/orderNumber/address.")] string? name_contains = null,
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("devices.list", null, null, cancellationToken);
        result = JsonFilters.WhereAnyFieldContains(result, name_contains, "name", "description", "comment", "product", "orderNumber", "address");
        return result;
    }

    [McpServerTool(Name = "knx_list_group_addresses", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List group addresses in the KNX project (ref, address, name, description, comment, DPT).

        Finding a GA by a human label (e.g. "Mittelgang"): the meaningful label is often in the
        `description` rather than the `name` (which may be a scheme like "WZ-3-Schalten"). So use
        `name_contains`: it matches case-insensitively across name/description/comment/address.

        Args:
            name_contains: Optional case-insensitive substring filter across
                name/description/comment/address.
        """)]
    public async Task<JsonNode?> ListGroupAddresses(
        [Description("Optional case-insensitive substring filter across name/description/comment/address.")] string? name_contains = null,
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("ga.list", null, null, cancellationToken);
        result = JsonFilters.WhereAnyFieldContains(result, name_contains, "name", "description", "comment", "address");
        return result;
    }

    [McpServerTool(Name = "knx_list_comobjects", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List communication objects (ComObjects) for a device -- the endpoints you link to
        group addresses.

        Each ComObject carries a DPT (datapoint type) and flags C/R/W/T/U (Communication,
        Read, Write, Transmit, Update). Reading these is what tells you what the device can
        actually do: e.g. an actuator's switch input is typically Write+Communication, its
        status output Read+Transmit. Only ACTIVE ComObjects are returned (and only active ones
        can be linked) -- a parameter change may activate/deactivate them, so re-read this after
        knx_set_parameter. When linking two ComObjects via a shared GA, their DPTs must be
        compatible. DPT may read back empty on some ETS versions.

        GROUPING BY FUNCTION/CHANNEL: prefer the `block` field -- the authoritative ETS
        group-object-tree path (e.g. "Operation / Display > Push button functions > PB9/10:
        Push buttons 9/10"). It is derived from the real UI tree and reliably identifies the
        function/block an object belongs to; group by it. If `block` is absent, fall back to:
        - `channel` (set when the product uses explicit SDK channels), else
        - MANY products (especially fixed-function multi-channel devices, e.g. an ABB blind
          actuator) do NOT expose SDK channels, so `channel` is absent. There the channel is
          in the object's `text`/`name` (e.g. "Output A" / "Ausgang A") and the specific
          function is in `functionText` (e.g. "Move blinds/shutter up-down"). Group by `text`
          (channel) and read `functionText` for the per-object function.
        So to see per-function object sets: group by `channel` if present, else by `text`, and
        use `functionText` for the function label. `name_contains` matches text/functionText/
        channel, so passing "Output A" scopes to that channel's objects.

        Fields include name/functionText/text/description. If a user-visible label is
        not among these, it is likely a parameter value -- check knx_list_parameters.

        Args:
            device_ref: The ref of the device (from knx_list_devices).
            name_contains: Optional case-insensitive substring filter. On a device with many
                ComObjects, pass e.g. "shutter" or "status" to return only matching objects
                (matches name/text/functionText/description AND the channel label, so a channel
                name like "Blind A" scopes to that function) instead of the full list.
        """)]
    public async Task<JsonNode?> ListComobjects(
        [Description("The ref of the device (from knx_list_devices).")] string device_ref,
        [Description("Optional case-insensitive substring filter. On a device with many ComObjects, pass e.g. \"shutter\" or \"status\" to return only matching objects (matches name/text/functionText/description AND the channel label, so a channel name like \"Blind A\" scopes to that function) instead of the full list.")] string? name_contains = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("comobjects.list", parameters, null, cancellationToken);
        result = JsonFilters.WhereAnyFieldContains(result, name_contains, "name", "text", "functionText", "description", "channel");
        return result;
    }

    [McpServerTool(Name = "knx_topology", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List the KNX topology: areas, lines, and segments.

        Returns the lineRef/segmentRef values needed for adding devices.
        """)]
    public async Task<JsonNode?> Topology(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("topology.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_catalog_manufacturers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List manufacturers available in the KNX product catalog.
        """)]
    public async Task<JsonNode?> CatalogManufacturers(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("catalog.manufacturers", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_catalog_search", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        Search the LOCALLY available catalog (project + downloaded products).

        Returns a catalogItemRef usable directly with knx_add_device. For products
        not yet on this machine, use knx_search_online_catalog instead.

        Args:
            query: matched against product name and order number.
            manufacturer_ref: optional manufacturer ref to restrict results.
        """)]
    public async Task<JsonNode?> CatalogSearch(
        [Description("matched against product name and order number.")] string query,
        [Description("optional manufacturer ref to restrict results.")] string? manufacturer_ref = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("query", query)
            .SetOptional("manufacturerRef", manufacturer_ref);
        var result = await bridge.CallAsync("catalog.search", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_search_online_catalog", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
        Search KNX's full ONLINE catalog (products not yet on this machine).

        Returns a unifiedCatalogItemRef; download it with knx_internalize_product
        (-> local catalogItemRef) before knx_add_device can use it. For products
        already installed locally, use knx_catalog_search instead.

        Args:
            query: matched against product name and order number.
            manufacturer_ref: optional manufacturer ref to restrict results.
        """)]
    public async Task<JsonNode?> SearchOnlineCatalog(
        [Description("matched against product name and order number.")] string query,
        [Description("optional manufacturer ref to restrict results.")] string? manufacturer_ref = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("query", query)
            .SetOptional("manufacturerRef", manufacturer_ref);
        var result = await bridge.CallAsync("catalog.search_online", parameters, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_list_parameters", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List application parameters for a device with values and semantics.

        Each parameter carries: value, isDefault, isActive, and (when the product data
        provides them) text (label), unit, access, options (allowed enum choices as
        value+text), min/max for numeric parameters, and `block` -- its UI path (e.g.
        "Operation / Display > Push button functions > PB9/10: Push buttons 9/10"). Use these to
        choose a valid value instead of guessing: for an enum parameter set one of
        `options[].value`; for a numeric one stay within min..max.

        TARGETING THE RIGHT INSTANCE: the same parameter name (e.g. "Key label for left push
        button") occurs MANY times on a multi-function device. `block` disambiguates them: to
        set a specific button's/channel's parameter, filter by `block` containing the UI node
        (e.g. "PB9/10") AND the parameter name, which yields exactly one parameterRef -- then
        knx_set_parameter that ref. Do NOT rely on ordering or guess a ref.

        Big devices can have thousands of parameters, most of them currently inactive. So
        `active_only` DEFAULTS TO TRUE -- you get only the parameters that are relevant right
        now. Pass active_only=False to include inactive ones (rarely needed; large). Combine
        with name_contains (case-insensitive, matches name/label) to fetch only what you need.

        IMPORTANT -- isActive and parameter dependencies: isActive is the result of ETS'
        visibility calculation. isActive=false means the parameter is currently hidden/
        irrelevant because another ("controlling") parameter has a value that deactivates it;
        setting it has no effect. The controlling condition itself is NOT exposed. The reliable
        way to handle dependencies is: change a parameter with knx_set_parameter, then call
        this tool again and observe which parameters flipped active/inactive. Setting a
        parameter can also (de)activate communication objects -- re-read knx_list_comobjects
        after a change.

        User-visible labels often live here as parameter VALUES (e.g. a push-button's
        'Text', or 'Description of objects'). Rename such a label with knx_set_parameter.

        Args:
            device_ref: The ref of the device (from knx_list_devices).
            active_only: Return only parameters with isActive=true. DEFAULTS TO TRUE; pass
                False to include currently-inactive parameters too.
            name_contains: Optional case-insensitive substring filter on name/label (text).
        """)]
    public async Task<JsonNode?> ListParameters(
        [Description("The ref of the device (from knx_list_devices).")] string device_ref,
        [Description("Return only parameters with isActive=true. DEFAULTS TO TRUE; pass False to include currently-inactive parameters too.")] bool active_only = true,
        [Description("Optional case-insensitive substring filter on name/label (text).")] string? name_contains = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("params.list", parameters, null, cancellationToken);
        if (active_only)
            result = JsonFilters.WhereTruthy(result, "isActive");
        result = JsonFilters.WhereAnyFieldContains(result, name_contains, "name", "text");
        return result;
    }
}
