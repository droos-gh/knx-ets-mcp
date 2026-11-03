// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Mutation tools.</summary>
[McpServerToolType]
public sealed class MutationTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_create_group_address", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a new KNX group address (a logical signal devices link to).

        Set the DPT to match the ComObjects you will link (e.g. 1.001 switch, 5.001 %, 3.007
        dim) -- a GA with the wrong DPT links but carries the wrong data. One function often
        needs several GAs (command + status, and for dimming also dim + value); create each
        with its matching DPT. Check the project's group-address style before choosing an
        address (knx_topology / existing GAs) so the new one fits the existing structure.

        Args:
            name: Human-readable name (e.g. "Light Kitchen").
            address: Three-level address string (e.g. "1/0/3").
            dpt_main: Main DPT number (e.g. 1 for switching). Optional.
            dpt_sub: Sub DPT number (e.g. 1 for DPT 1.001). Optional.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> CreateGroupAddress(
        [Description("Human-readable name (e.g. \"Light Kitchen\").")] string name,
        [Description("Three-level address string (e.g. \"1/0/3\").")] string address,
        [Description("Main DPT number (e.g. 1 for switching). Optional.")] int? dpt_main = null,
        [Description("Sub DPT number (e.g. 1 for DPT 1.001). Optional.")] int? dpt_sub = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("name", name)
            .Set("address", address)
            .SetOptional("dptMain", dpt_main)
            .SetOptional("dptSub", dpt_sub);
        var result = await bridge.CallAsync("ga.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_add_device", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Add a device from the product catalog to a topology line.

        Find the catalog item first with knx_catalog_search / knx_browse_products (or import a
        .knxprod with knx_import_product). The device's medium must match the line's medium
        (a TP product cannot go on an RF line) and the address must be free on that line. The
        new device arrives with the product's DEFAULT parameters and ComObjects already set;
        then follow the usual order: set parameters -> re-read knx_list_comobjects -> create/
        link GAs -> knx_program_device. Consult the device's technical manual (ask the user for
        the PDF) to parametrize correctly.

        Args:
            line_ref: The ref of the target line.
            catalog_item_ref: The catalog item ref for the device product.
            address: Physical address for the device (e.g. "1.1.3").
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> AddDevice(
        [Description("The ref of the target line.")] string line_ref,
        [Description("The catalog item ref for the device product.")] string catalog_item_ref,
        [Description("Physical address for the device (e.g. \"1.1.3\").")] string address,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("lineRef", line_ref)
            .Set("catalogItemRef", catalog_item_ref)
            .Set("address", address);
        var result = await bridge.CallAsync("device.addFromCatalog", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_link", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Link a communication object to a group address (the core act of wiring KNX logic).

        A GA is the shared signal: link every ComObject that should talk on it to the same GA
        (e.g. a switch sensor's output and an actuator's input on one "Light Kitchen" GA).
        Preconditions that make this succeed: the ComObject must be ACTIVE (set the controlling
        parameters first, then re-read knx_list_comobjects), and its DPT must be compatible with
        the GA / the other linked ComObjects. Linking an inactive ComObject fails on some ETS
        versions. Set parameters BEFORE linking, not after.

        Returns {comObjectRef, gaRef, dpt?, gaDpt?, dptWarning?}. The link always proceeds, but
        if dptWarning is present the com-object and GA carry different data types (e.g. a %
        object on a switch GA) -- surface it and fix the wiring rather than ignoring it.

        Args:
            com_object_ref: The ref of the communication object.
            ga_ref: The ref of the group address.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> Link(
        [Description("The ref of the communication object.")] string com_object_ref,
        [Description("The ref of the group address.")] string ga_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("comObjectRef", com_object_ref)
            .Set("gaRef", ga_ref);
        var result = await bridge.CallAsync("link.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_unlink", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Remove a link between a communication object and a group address.

        Args:
            com_object_ref: The ref of the communication object.
            ga_ref: The ref of the group address.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> Unlink(
        [Description("The ref of the communication object.")] string com_object_ref,
        [Description("The ref of the group address.")] string ga_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("comObjectRef", com_object_ref)
            .Set("gaRef", ga_ref);
        var result = await bridge.CallAsync("link.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_set_parameter", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Set an application parameter on a device.

        Pick the value from knx_list_parameters: for an enum parameter use one of
        options[].value; for a numeric one stay within min..max. Setting a parameter whose
        isActive is false has no effect (it is deactivated by a controlling parameter -- change
        that one instead). A parameter change can (de)activate ComObjects and change the memory
        layout, so ALWAYS re-read knx_list_comobjects (and knx_list_parameters) afterwards
        before linking or setting flags. To understand what a parameter actually does (beyond
        its label), consult the device's technical manual (see the server instructions).

        Args:
            device_ref: The ref of the device.
            parameter_ref: The ref of the parameter.
            value: The new value (as string).
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> SetParameter(
        [Description("The ref of the device.")] string device_ref,
        [Description("The ref of the parameter.")] string parameter_ref,
        [Description("The new value (as string).")] string value,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("deviceRef", device_ref)
            .Set("parameterRef", parameter_ref)
            .Set("value", value);
        var result = await bridge.CallAsync("param.set", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_import_product", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Import a .knxprod product file into the local KNX catalog.

        The file must be a signed .knxprod on the ETS host.  After import the
        product is available via knx_catalog_search / knx_add_device.

        Args:
            path: Absolute path to the .knxprod file on the ETS host.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> ImportProduct(
        [Description("Absolute path to the .knxprod file on the ETS host.")] string path,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("path", path);
        var result = await bridge.CallAsync("catalog.import", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_internalize_product", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Download an online catalog product into the local KNX catalog.

        Takes a unifiedCatalogItemRef from knx_search_online_catalog and fetches
        the product data locally.  Returns the local catalogItemRef that can then
        be used with knx_catalog_search / knx_add_device.

        Args:
            unified_catalog_item_ref: The ref from the online catalog search.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> InternalizeProduct(
        [Description("The ref from the online catalog search.")] string unified_catalog_item_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("unifiedCatalogItemRef", unified_catalog_item_ref);
        var result = await bridge.CallAsync("catalog.internalize", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
