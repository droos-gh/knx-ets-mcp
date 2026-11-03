# KNX ETS Bridge -- MCP Server

MCP server that exposes KNX ETS6 project operations as tools for LLMs.
Built with [FastMCP v3](https://gofastmcp.com/) (`from fastmcp import FastMCP`).
It forwards each tool call as a newline-delimited JSON request over IPC
to the ETS6 AddIn (running inside ETS on Windows), then returns the response.

## Requirements

- Python 3.11+
- [uv](https://docs.astral.sh/uv/)

## Setup

```bat
cd mcp-server
uv sync --all-extras
```

## Running

```bat
:: Production (connects to the ETS AddIn via named pipe):
set "KNX_BRIDGE_TRANSPORT=pipe"
uv run python -m knx_ets_mcp.server

:: Development / testing (in-memory mock AddIn, no ETS needed):
set "KNX_BRIDGE_TRANSPORT=mock"
uv run python -m knx_ets_mcp.server

:: Via the FastMCP CLI (alternative):
set "KNX_BRIDGE_TRANSPORT=mock"
uv run fastmcp run src/knx_ets_mcp/server.py:mcp
```

By default the server listens on **Streamable HTTP** at `http://0.0.0.0:8765/mcp`
(`MCP_HOST` / `MCP_PORT` / `MCP_PATH`). A network bind always requires a bearer token:
set `MCP_AUTH_TOKEN`, or the server generates one on first start, stores it in
`%LOCALAPPDATA%\knx-ets-mcp\http-token` and prints it. A loopback bind
(`MCP_HOST=127.0.0.1`) needs no token. Set `MCP_TRANSPORT=stdio` when the AI client
launches the server itself (as in the Claude Desktop config below).

### Claude Desktop configuration

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "knx-ets-bridge": {
      "command": "uv",
      "args": ["run", "--directory", "C:\\path\\to\\mcp-server", "python", "-m", "knx_ets_mcp.server"],
      "env": {
        "MCP_TRANSPORT": "stdio",
        "KNX_BRIDGE_TRANSPORT": "mock"
      }
    }
  }
}
```

Set `KNX_BRIDGE_TRANSPORT` to `pipe` (or remove that line) when running against a real
ETS6 instance; keep `MCP_TRANSPORT=stdio`.

## Tests

```bat
uv run pytest -v
```

All tests run against `MockTransport` via FastMCP's in-memory `Client`,
exercising the full MCP tool layer end-to-end -- no Windows or ETS6
required.

## Tool list

| Tool | Protocol method | Type |
|---|---|---|
| `knx_project_info` | `project.info` | read |
| `knx_list_devices` | `devices.list` | read |
| `knx_list_group_addresses` | `ga.list` | read |
| `knx_list_comobjects` | `comobjects.list` | read |
| `knx_create_group_address` | `ga.create` | mutation |
| `knx_add_device` | `device.addFromCatalog` | mutation |
| `knx_link` | `link.create` | mutation |
| `knx_unlink` | `link.delete` | mutation |
| `knx_set_parameter` | `param.set` | mutation |
| `knx_program_device` | `device.program` | programming |
| `knx_job_status` | `job.status` | programming |
| `knx_job_cancel` | `job.cancel` | programming |

## Idempotency

Every mutation tool call includes an `idempotencyKey` in the protocol
request.  The key is a **fresh UUID4** generated per operation.  If the
client layer retries the same operation after a transport failure, it
reuses the same key so the AddIn deduplicates the retry correctly.

Two separate tool calls with identical arguments get **different** keys
and execute independently.  This ensures sequences like
link -> unlink -> link work correctly (the second link is not
deduplicated against the first).

## Architecture

```
LLM  --(MCP/HTTP|stdio)-->  MCPServer  --(client)-->  Transport  --(JSON/pipe|tcp)-->  ETS6 AddIn
                             server.py                 client.py                       (C#, in ETS)
                                                       |
                                                       +-- NamedPipeTransport (prod, same PC)
                                                       +-- TcpTransport (prod, remote ETS)
                                                       +-- MockTransport (tests)
```

## Transport selection

Set `KNX_BRIDGE_TRANSPORT` environment variable:

| Value | Transport | Platform |
|---|---|---|
| `pipe` (default) | `NamedPipeTransport` | Windows only |
| `tcp` | `TcpTransport` (`KNX_BRIDGE_PORT` + `KNX_BRIDGE_TOKEN` required, `KNX_BRIDGE_HOST` default `127.0.0.1`) | Any |
| `mock` | `MockTransport` | Any (in-memory fake) |
