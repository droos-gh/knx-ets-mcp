# KNX ETS Bridge -- MCP Server (.NET 10)

MCP server that exposes KNX ETS 5/6 project operations as tools for LLMs. Built with the
official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
(`ModelContextProtocol` + `ModelContextProtocol.AspNetCore`). It forwards each tool call as
a newline-delimited JSON request (docs/protocol.md) to the ETS AddIn running inside ETS on
Windows, then returns the AddIn's result.

## Requirements

- To **run**: nothing — the release ships `Start-MCP-Server.exe`, a self-contained
  single-file Windows exe.
- To **build/test** from source: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

## Build and test

```bat
dotnet build mcp-server\src\KnxEtsMcp -c Release
dotnet test mcp-server\tests\KnxEtsMcp.Tests -c Release
```

All tests run against `MockTransport` through an in-memory MCP client, exercising the full
MCP tool layer end to end -- no ETS required.

Publish the standalone exe (what the release does):

```bat
dotnet publish mcp-server\src\KnxEtsMcp -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## Running

By default the server listens on **Streamable HTTP** at `http://0.0.0.0:8765/mcp`
(`MCP_HOST` / `MCP_PORT` / `MCP_PATH`). A network bind always requires a bearer token:
set `MCP_AUTH_TOKEN`, or the server generates one on first start, stores it in
`%LOCALAPPDATA%\knx-ets-mcp\http-token` (`MCP_AUTH_TOKEN_FILE` to override) and prints it. A
loopback bind (`MCP_HOST=127.0.0.1`) needs no token. `MCP_ALLOW_NO_AUTH=1` disables auth
on a network bind (trusted LAN only). Set `MCP_TRANSPORT=stdio` when the AI client
launches the server itself.

```bat
:: Production (connects to the ETS AddIn via named pipe):
knx-ets-mcp.exe

:: Development (in-memory mock AddIn, no ETS needed), from source:
set "KNX_BRIDGE_TRANSPORT=mock"
dotnet run --project mcp-server\src\KnxEtsMcp
```

### Claude Desktop configuration (stdio)

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "knx-ets-bridge": {
      "command": "C:\\Tools\\knx-ets-mcp.exe",
      "env": {
        "MCP_TRANSPORT": "stdio"
      }
    }
  }
}
```

Add `"KNX_BRIDGE_TRANSPORT": "mock"` to `env` to try it without ETS.

## Layout

```
src/KnxEtsMcp/
  Program.cs                     entry point: settings, HTTP (auth middleware) or stdio host
  Application/
    ServerSettings.cs            environment variables -> settings, transport selection
    HttpAuth.cs                  bearer-token resolution (generated + persisted) and check
    ServerInstructions.cs        instructions sent to the client (the LLM's operating guide)
    McpServerSetup.cs            registers all tool classes
    Tools/*.cs                   the 105 MCP tools ([McpServerTool], thin adapters)
  BusinessLogic/
    Gateways/IKnxBridge.cs       the AddIn as seen by the tools
    Result.cs, ProtocolMethods.cs
  Adapters/
    Bridge/KnxBridgeClient.cs    envelope, idempotency keys, one reconnect+retry, serialization
    Transport/                   NamedPipeTransport, TcpTransport, MockTransport (Mock/)
tests/KnxEtsMcp.Tests/           xUnit tests (Support/McpTestHost.cs = in-memory MCP client)
```

```
LLM  --(MCP/HTTP|stdio)-->  Tools  -->  KnxBridgeClient  -->  ITransport  --(JSON/pipe|tcp)-->  ETS AddIn
                                                              +-- NamedPipeTransport (prod, same PC)
                                                              +-- TcpTransport (prod, remote ETS)
                                                              +-- MockTransport (tests, development)
```

Tool names and argument names are snake_case (`knx_list_comobjects`, `device_ref`,
`expected_project_revision`, ...), unchanged from the former Python server. A failed call
returns an MCP tool error whose text is `[code] message` (protocol error codes from
docs/protocol.md).

## Transport selection

Set the `KNX_BRIDGE_TRANSPORT` environment variable:

| Value | Transport | Platform |
|---|---|---|
| `pipe` (default) | `NamedPipeTransport` (reads `%LOCALAPPDATA%\knx-ets-bridge\session.json`) | Windows only |
| `tcp` | `TcpTransport` (`KNX_BRIDGE_PORT` + `KNX_BRIDGE_TOKEN` required, `KNX_BRIDGE_HOST` default `127.0.0.1`) | Any |
| `mock` | `MockTransport` | Any (in-memory fake) |

## Idempotency

Every mutating call carries an `idempotencyKey`: a **fresh UUID** per operation. If the
client retries the same operation after a transport failure, it reuses the same key so the
AddIn deduplicates the retry. Two separate tool calls with identical arguments get
**different** keys and execute independently, so link -> unlink -> link works.
