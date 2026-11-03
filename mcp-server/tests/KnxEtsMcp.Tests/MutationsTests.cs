using KnxEtsMcp.BusinessLogic;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>Mutation tools over the MCP layer (port of tests/test_mutations.py).</summary>
public sealed class MutationsTests
{
    // -- knx_create_group_address ---------------------------------------------

    [Fact]
    public async Task CreateGroupAddress()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Light Kitchen"),
            ("address", "1/0/3"),
            ("dpt_main", 1),
            ("dpt_sub", 1))));
        Assert.True(data.ContainsKey("ref"));
        Assert.Equal("1/0/3", data.StringAt("address"));

        // Verify it shows up in the list
        var gas = ToolAssert.Array(await host.CallAsync("knx_list_group_addresses"));
        var newGa = gas.TryFindBy("address", "1/0/3");
        Assert.NotNull(newGa);
        Assert.Equal("Light Kitchen", newGa.StringAt("name"));
        Assert.Equal("1.001", newGa.StringAt("dpt"));
    }

    [Fact]
    public async Task CreateGroupAddressDuplicate()
    {
        // Creating a GA with an existing address must fail.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Duplicate"),
            ("address", "1/0/1")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- knx_add_device -------------------------------------------------------

    [Fact]
    public async Task AddDevice()
    {
        // device.addFromCatalog returns {ref, address} and bumps revision.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_add_device", Args.Of(
            ("line_ref", "line-1.1"),
            ("catalog_item_ref", "cat-abb-sa4"),
            ("address", "1.1.5"))));
        Assert.True(data.ContainsKey("ref"));
        Assert.Equal("1.1.5", data.StringAt("address"));

        // Verify the device shows up in the list
        var devices = ToolAssert.Array(await host.CallAsync("knx_list_devices"));
        var newDev = devices.TryFindBy("address", "1.1.5");
        Assert.NotNull(newDev);
        Assert.Equal(data.StringAt("ref"), newDev.StringAt("ref"));
    }

    [Fact]
    public async Task AddDeviceUnknownCatalogItem()
    {
        // device.addFromCatalog with unknown catalog item returns not_found.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_add_device", Args.Of(
            ("line_ref", "line-1.1"),
            ("catalog_item_ref", "DOES-NOT-EXIST"),
            ("address", "1.1.6")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task AddDeviceUnknownLine()
    {
        // device.addFromCatalog with unknown line returns not_found.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_add_device", Args.Of(
            ("line_ref", "line-NOPE"),
            ("catalog_item_ref", "cat-abb-sa4"),
            ("address", "1.1.7")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task AddDeviceDuplicateAddress()
    {
        // device.addFromCatalog with a taken address returns invalid_params.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_add_device", Args.Of(
            ("line_ref", "line-1.1"),
            ("catalog_item_ref", "cat-abb-sa4"),
            ("address", "1.1.1")));  // already used by seed device
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- knx_link -------------------------------------------------------------

    [Fact]
    public async Task LinkActiveObject()
    {
        // Linking an active com-object to a GA should succeed.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_link", Args.Of(
            ("com_object_ref", "co-0002"),
            ("ga_ref", "ga-0001"))));
        Assert.Equal("co-0002", data.StringAt("comObjectRef"));
        Assert.Equal("ga-0001", data.StringAt("gaRef"));
        // co-0002 (1.001) and ga-0001 (1.001) match -> no DPT warning.
        Assert.False(data.ContainsKey("dptWarning"));

        // Verify the link appears
        var cos = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        var co2 = cos.FindBy("ref", "co-0002");
        Assert.Contains("ga-0001", co2.ArrayAt("links").Strings());
    }

    [Fact]
    public async Task LinkDptMismatchWarns()
    {
        // Linking DPTs with different main numbers still succeeds but returns dptWarning.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_link", Args.Of(
            ("com_object_ref", "co-0004"),  // dpt 5.001 (%)
            ("ga_ref", "ga-0001")));        // dpt 1.001 (switch)
        Assert.False(result.IsError == true);
        var data = ToolAssert.Object(result);
        Assert.Equal("5.001", data.StringAt("dpt"));
        Assert.Equal("1.001", data.StringAt("gaDpt"));
        Assert.True(data.ContainsKey("dptWarning"));
        Assert.Contains("mismatch", (data.StringAt("dptWarning") ?? "").ToLowerInvariant());
        // The link still happened despite the warning.
        var cos = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0002"))));
        var co4 = cos.FindBy("ref", "co-0004");
        Assert.Contains("ga-0001", co4.ArrayAt("links").Strings());
    }

    [Fact]
    public async Task LinkInactiveObject()
    {
        // Linking an inactive com-object must fail with inactive_object.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_link", Args.Of(
            ("com_object_ref", "co-0003"),
            ("ga_ref", "ga-0001")));
        Assert.Contains("inactive_object", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task LinkNonexistentComobject()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_link", Args.Of(
            ("com_object_ref", "co-9999"),
            ("ga_ref", "ga-0001")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    [Fact]
    public async Task LinkNonexistentGa()
    {
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_link", Args.Of(
            ("com_object_ref", "co-0002"),
            ("ga_ref", "ga-9999")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_unlink -----------------------------------------------------------

    [Fact]
    public async Task Unlink()
    {
        // Removing an existing link should succeed.
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_unlink", Args.Of(
            ("com_object_ref", "co-0001"),
            ("ga_ref", "ga-0001"))));
        Assert.True(data.BoolAt("ok"));

        var cos = ToolAssert.Array(await host.CallAsync("knx_list_comobjects", Args.Of(("device_ref", "dev-0001"))));
        var co1 = cos.FindBy("ref", "co-0001");
        Assert.DoesNotContain("ga-0001", co1.ArrayAt("links").Strings());
    }

    [Fact]
    public async Task UnlinkAbsentIsIdempotent()
    {
        // Unlinking a valid but unlinked com-object/GA pair is idempotent (ok, no error).
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_unlink", Args.Of(
            ("com_object_ref", "co-0002"),
            ("ga_ref", "ga-0001"))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task UnlinkUnknownRef()
    {
        // Unlinking with an unknown com-object ref returns not_found.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_unlink", Args.Of(
            ("com_object_ref", "co-9999"),
            ("ga_ref", "ga-0001")));
        Assert.Contains("not_found", ToolAssert.ErrorText(result));
    }

    // -- knx_set_parameter ----------------------------------------------------

    [Fact]
    public async Task SetParameter()
    {
        await using var host = await McpTestHost.StartAsync();
        var data = ToolAssert.Object(await host.CallAsync("knx_set_parameter", Args.Of(
            ("device_ref", "dev-0001"),
            ("parameter_ref", "param-0001"),
            ("value", "eco"))));
        Assert.True(data.BoolAt("ok"));
    }

    [Fact]
    public async Task SetParameterWrongDevice()
    {
        // Parameter must belong to the specified device.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_set_parameter", Args.Of(
            ("device_ref", "dev-0002"),
            ("parameter_ref", "param-0001"),
            ("value", "eco")));
        Assert.Contains("invalid_params", ToolAssert.ErrorText(result));
    }

    // -- expected_project_revision --------------------------------------------

    [Fact]
    public async Task MutationWithMatchingRevision()
    {
        // Passing the correct expected_project_revision allows the mutation.
        await using var host = await McpTestHost.StartAsync();
        var currentRev = host.Mock.Revision;
        var data = ToolAssert.Object(await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Revision-guarded GA"),
            ("address", "5/0/1"),
            ("expected_project_revision", currentRev))));
        Assert.Equal("5/0/1", data.StringAt("address"));
    }

    [Fact]
    public async Task MutationWithStaleRevision()
    {
        // Passing a stale expected_project_revision returns revision_mismatch.
        await using var host = await McpTestHost.StartAsync();
        var result = await host.CallAsync("knx_create_group_address", Args.Of(
            ("name", "Stale GA"),
            ("address", "5/0/2"),
            ("expected_project_revision", "stale-rev-000")));
        Assert.Contains("revision_mismatch", ToolAssert.ErrorText(result));
    }

    // -- mutating-methods completeness (Python _MUTATING_METHODS -> ProtocolMethods.Mutating) --

    [Fact]
    public void MutatingMethodsContainBusSetIndividualAddress()
    {
        // bus.setIndividualAddress must be mutating, for idempotency.
        Assert.Contains("bus.setIndividualAddress", ProtocolMethods.Mutating);
    }

    [Fact]
    public void MutatingMethodsContainDeviceReset()
    {
        // device.reset must be mutating, for idempotency.
        Assert.Contains("device.reset", ProtocolMethods.Mutating);
    }
}
