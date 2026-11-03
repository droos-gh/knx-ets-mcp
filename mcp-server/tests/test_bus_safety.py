"""Bus-write safety: MCP tool annotations and the AddIn's "Bus read-only" mode."""

from __future__ import annotations

import pytest
from fastmcp import Client

from knx_ets_mcp.client import KnxBridgeClient
from knx_ets_mcp.server import _reset_client, _set_client, mcp as mcp_server
from knx_ets_mcp.transport.mock import BUS_WRITE_METHODS, MockTransport

from tests.conftest import error_text, tool_data

# Every tool that ends in a protocol method from BUS_WRITE_METHODS.
BUS_WRITE_TOOLS = {
    "knx_program_device",
    "knx_update_firmware",
    "knx_group_write",
    "knx_device_unload",
    "knx_device_reset",
    "knx_bus_set_individual_address",
}

BUS_READ_TOOLS = {
    "knx_bus_ping",
    "knx_bus_scan_line",
    "knx_reconstruct_line",
    "knx_read_group_objects",
    "knx_device_read_info",
    "knx_group_read",
    "knx_group_monitor",
}


async def _tools():
    async with Client(mcp_server) as c:
        return {t.name: t for t in await c.list_tools()}


# -- Annotations ---------------------------------------------------------------


async def test_every_tool_is_annotated() -> None:
    tools = await _tools()
    missing = [name for name, t in tools.items() if t.annotations is None
               or t.annotations.readOnlyHint is None
               or t.annotations.destructiveHint is None]
    assert not missing, f"tools without safety annotations: {missing}"


async def test_bus_write_tools_are_destructive_and_open_world() -> None:
    tools = await _tools()
    for name in BUS_WRITE_TOOLS | {"knx_job_cancel"}:
        ann = tools[name].annotations
        assert ann.readOnlyHint is False, name
        assert ann.destructiveHint is True, name
        assert ann.openWorldHint is True, name


async def test_bus_read_tools_are_read_only() -> None:
    tools = await _tools()
    for name in BUS_READ_TOOLS:
        ann = tools[name].annotations
        assert ann.readOnlyHint is True, name
        assert ann.openWorldHint is True, name


async def test_project_reads_are_read_only_and_deletes_destructive() -> None:
    tools = await _tools()
    for name in ("knx_list_devices", "knx_list_group_addresses", "knx_list_parameters"):
        assert tools[name].annotations.readOnlyHint is True, name
    for name in ("knx_delete_device", "knx_delete_group_address", "knx_unlink",
                 "knx_set_parameter", "knx_batch_apply"):
        ann = tools[name].annotations
        assert ann.readOnlyHint is False, name
        assert ann.destructiveHint is True, name
    ann = tools["knx_create_group_address"].annotations
    assert ann.readOnlyHint is False and ann.destructiveHint is False


async def test_only_bus_tools_are_open_world_besides_online_catalog() -> None:
    tools = await _tools()
    open_world = {name for name, t in tools.items() if t.annotations.openWorldHint}
    assert open_world == BUS_WRITE_TOOLS | BUS_READ_TOOLS | {
        "knx_job_cancel", "knx_search_online_catalog"}


# -- Bus read-only mode (mock mirrors the AddIn's BusWritePolicy) --------------


@pytest.fixture
async def read_only_client():
    transport = MockTransport(bus_read_only=True)
    await transport.connect()
    _set_client(KnxBridgeClient(transport))
    try:
        async with Client(mcp_server) as c:
            yield c
    finally:
        _reset_client()


async def test_read_only_blocks_programming(read_only_client) -> None:
    result = await read_only_client.call_tool(
        "knx_program_device", {"device_ref": "dev-0001"}, raise_on_error=False)
    text = error_text(result)
    assert "[approval_required]" in text
    assert "Bus read-only" in text


async def test_read_only_blocks_group_write(read_only_client) -> None:
    result = await read_only_client.call_tool(
        "knx_group_write", {"ga_ref": "ga-0001", "value": "01"}, raise_on_error=False)
    assert "Bus read-only" in error_text(result)


async def test_read_only_still_allows_bus_reads(read_only_client) -> None:
    result = await read_only_client.call_tool(
        "knx_bus_ping", {"address": "1.1.1"}, raise_on_error=False)
    assert tool_data(result)["alive"] is True


async def test_read_only_still_allows_project_edits(read_only_client) -> None:
    result = await read_only_client.call_tool(
        "knx_create_group_address",
        {"name": "Kitchen light", "address": "1/1/99"},
        raise_on_error=False,
    )
    tool_data(result)


async def test_programming_allowed_by_default(client) -> None:
    result = await client.call_tool(
        "knx_program_device", {"device_ref": "dev-0001"}, raise_on_error=False)
    tool_data(result)


def test_mock_bus_write_set_matches_tools() -> None:
    assert BUS_WRITE_METHODS == {
        "device.program", "firmware.update", "group.write",
        "device.unload", "device.reset", "bus.setIndividualAddress",
    }
