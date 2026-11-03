# knx-ets-mcp

Control KNX **ETS 5/6** from an AI coding assistant. `knx-ets-mcp` connects
MCP-capable assistants — **Claude Code**, **Codex**, and the like — to your open ETS
project so they can help you **create a new project, maintain an existing one, or tidy one
up**: inspect it, build and edit it (devices, group addresses, links, parameters), browse
and update the product catalog, and program devices — all through ETS's own engine.

> **Not an official KNX product.** "KNX" and "ETS" are trademarks of the KNX
> Association. This is an independent, experimental community tool. Use at your own risk;
> do not point it at a production installation you cannot afford to disturb.

## What it does

It has two parts that work together:

1. An **ETS 5/6 AddIn** that runs inside ETS on Windows and exposes a safe, fixed set of
   operations on your project — reachable over a local **named pipe** (same machine) or
   an optional **TCP** endpoint (so the MCP server can run on a different machine).
2. An **MCP server** that your AI assistant connects to; it forwards the assistant's
   requests to the AddIn over that named pipe (same machine) or TCP (across your LAN).

Your assistant then has tools to:

- **Inspect** — devices (with order numbers), group addresses, communication objects
  (with their function/channel block), topology, building view, catalog, parameters
  (with labels, units, allowed options, value ranges, and their UI block so the right
  parameter can be targeted reliably on complex devices), channels/modules, trades, tags,
  to-dos, project history, and KNX Secure certificates.
- **Build** — create group addresses, add devices from the catalog, link communication
  objects to group addresses, set parameters (and reset to defaults).
- **Edit** — delete/rename group addresses, set descriptions and datapoint types, create
  and delete group ranges, delete/rename devices, change a device's individual address,
  create/delete areas/lines/segments, set communication-object flags (C/R/W/T/U +
  priority), move GAs/group ranges/lines/building parts between parents.
- **Building view** — create buildings/floors/rooms/cabinets, assign devices to them, and
  create building functions linked to group addresses.
- **Trades / organization** — create/delete trades, assign devices; manage tags, to-do
  items, and project history entries.
- **Bus interface** — link/unlink group addresses on couplers (filter table).
- **Additional addresses** — add/remove additional individual addresses on devices and
  additional/sending group addresses on lines.
- **Catalog** — search the online catalog, browse a manufacturer's products, read product
  details, pull in online products, or import a manufacturer `.knxprod` file.
- **KNX Secure** — list, add, and delete device certificates.
- **Project** — export (`.knxproj`), undo/redo, navigate to any object in the ETS UI.
- **Bus / online** (KNXnet/IP): ping, scan a line for devices, read a device's
  mask version, read/write group values, monitor group telegrams, reconstruct a line's
  device inventory, program individual addresses onto the bus, and reset devices.
- **Recover from the bus** — scan a line, read each device's group-object
  associations (`device.readGroupObjects`), and observe traffic to reverse-engineer an
  existing installation. The MCP pulls raw data; the LLM composes the project from it.
  See [docs/protocol.md](docs/protocol.md#project-recovery-reverse-engineering-from-the-bus).
- **Program devices** — download a device's configuration (individual address, group
  address table, parameters, application) over KNXnet/IP using ETS's own load engine.
  Reversible: re-program with a corrected configuration.
- **Update firmware** — flash device firmware (IoT devices only). Potentially
  irreversible, so it is gated behind an explicit preference (off by default).

Every project change is a normal ETS undo step. Built-in safety: retry-safe mutations
(no accidental duplicates via idempotency keys), an optional guard against changes you
made in ETS at the same time (expected project revision), and a preference gate for
firmware.

## Requirements

- **ETS 5 or ETS 6** (edition **Lite or higher** -- the free Demo cannot load AddIns).
  The release ships builds for both **ETS 6** and **ETS 5**; `install.bat` picks the right
  one per installed ETS version. Because this is a sideloaded (non-validated) AddIn, ETS
  runs it only with a (free) **ETS App developer/test license** (bound to a KNX organization
  account) or with the ETS license check disabled; ETS itself must be licensed (Lite or
  higher).
- **Windows** (x64) to run ETS.
- A **bus interface** configured in ETS for bus operations (KNXnet/IP or USB).
- An **MCP-capable AI client** (e.g. Claude Desktop or Claude Code).
- To run the MCP server (recommended: on the ETS machine): nothing extra — the bundled
  `knx-ets-mcp.exe` ships its own Python and can serve your LAN over HTTP. Your other
  machines then need only an MCP client; they connect to a URL, no Python required.
- Only if you run the server on a *different* machine than ETS (option C below):
  **Python 3.11+** there, ideally with [`uv`](https://docs.astral.sh/uv/).

## Setup (step by step)

You do **not** need to build anything — download the release artifacts.

### 1. Download the latest release

From the [GitHub **Releases** page](https://github.com/knx-ai/knx-ets-mcp/releases),
download the single bundle **`knx-ets-mcp-<version>.zip`**. It contains everything:

- the ETS AddIn (for ETS 5 and ETS 6) + the installer, and
- the MCP server as a standalone Windows `.exe` (bundles Python).

**Extract the whole zip first** (right-click the zip → *Extract All*). Inside you get:

- **`install.bat`** — installs the ETS AddIn.
- **`Start-MCP-Server.exe`** — the MCP server (double-click to run).

(A Python wheel is also included under `mcp-server-python/`, and attached to the release
separately, for those who prefer to run the server with their own Python.)

> **Do not run `install.bat` from inside the zip.** Windows' built-in zip viewer extracts
> only the single file you click to a temp folder, so the installer can't find the AddIn
> payload next to it (it will tell you to extract first). Extract the whole zip, then run
> `install.bat` from the extracted folder. (WinZip users get a shortcut: its "Unzip and
> Install" recognizes the `install` program and extracts everything automatically.)

### 2. Install the ETS AddIn (on Windows)

1. Close ETS.
2. Right-click `knx-ets-mcp-<version>.zip` → **Extract All** (do not run from inside the zip).
3. Double-click **`install.bat`** inside the extracted folder. It copies the
   AddIn — including its ETS directory signature (`<AppId>.signature`, so ETS
   treats the folder as signed) — into the correct (hidden) location for each
   installed ETS version (`C:\ProgramData\KNX\ETS<n>\Apps\AddIns\<AppId>\`) and
   clears the AddIn cache, then prints how to start the MCP server (next step).
   If Windows reports an access error, right-click it and choose *Run as
   administrator*.
4. Start ETS.

To remove it later, run `uninstall.bat` from the same folder.

### 3. Open your project

Open the ETS project you want to work on. The AddIn shows a status panel and, once active,
is ready to accept requests. Keep ETS and the project open while you work. Nothing else to
configure here for the recommended setup — the separate ETS **TCP endpoint** is only for
the off-box variant (option C below).

### 4. Run the MCP server

The server has two independent transports: the **client-facing** side (how your AI client
reaches the server — stdio, or Streamable HTTP over the network) and the **AddIn-facing**
side (how the server reaches ETS — the local named pipe, or the ETS TCP endpoint). Pick
the option that matches where things run.

#### A. Recommended: server on the ETS machine, reachable over your network

On the ETS machine, just double-click **`Start-MCP-Server.exe`** from the extracted
folder. **By default it serves Streamable HTTP on `0.0.0.0:8765/mcp`** (reachable on your
LAN), **protected by a bearer token**, and talks to the AddIn over the local named pipe —
no ETS TCP endpoint, no env vars to set. (The examples below call it by its internal name
`knx-ets-mcp.exe`; for advanced options run it from a terminal.)

On first start the server generates a random token, stores it for your Windows user
(`%LOCALAPPDATA%\knx-ets-mcp\http-token`, reused on every later start) and prints it
together with the ready-to-paste `claude mcp add` command:

```bat
:: Windows, on the ETS machine
knx-ets-mcp.exe
:: -> [knx-ets-mcp] Streamable HTTP on http://0.0.0.0:8765/mcp (auth: on -- C:\Users\you\AppData\Local\knx-ets-mcp\http-token)
:: -> [knx-ets-mcp] Bearer token: <generated-token>
```

Change the bind with `MCP_HOST` / `MCP_PORT` / `MCP_PATH` if needed; `KNX_BRIDGE_TRANSPORT`
defaults to the local pipe, so you don't set it.

**Choose your own token** instead by setting `MCP_AUTH_TOKEN` (or point
`MCP_AUTH_TOKEN_FILE` at another file). Either way the endpoint requires
`Authorization: Bearer <token>`:

```bat
set "MCP_AUTH_TOKEN=choose-a-long-random-secret"
knx-ets-mcp.exe
:: -> [knx-ets-mcp] Streamable HTTP on http://0.0.0.0:8765/mcp (auth: on -- MCP_AUTH_TOKEN)
```

Then, from any machine on your LAN, point Claude Code at the server's URL (use the
ETS machine's IP) with the token:

```bat
claude mcp add --scope user --transport http knx-ets http://<ets-pc-ip>:8765/mcp --header "Authorization: Bearer <token>"
```

…or a portable project `.mcp.json` (change the host to your Windows IP and paste the
token; this repo's own `.mcp.json` targets a loopback server, which needs no token):

```json
{
  "mcpServers": {
    "knx-ets": {
      "type": "http",
      "url": "http://<windows-ip>:8765/mcp",
      "headers": { "Authorization": "Bearer choose-a-long-random-secret" }
    }
  }
}
```

Security: a network bind always requires the token. Only a **loopback** bind
(`MCP_HOST=127.0.0.1`, same machine only) runs without one. `MCP_ALLOW_NO_AUTH=1` turns
authentication off on a network bind — then anyone who can reach the port can control ETS
and program devices; use it only on an isolated, trusted LAN. There is **no TLS** either
way, so keep the server on a trusted network.

#### B. Everything on one machine (local only)

If the AI client runs on the same Windows machine as ETS and you'd rather have the client
launch the server, use stdio. The exe defaults to HTTP, so tell it to speak stdio with
`MCP_TRANSPORT=stdio`:

```json
{
  "mcpServers": {
    "knx-ets": {
      "command": "C:\\Tools\\knx-ets-mcp.exe",
      "env": { "MCP_TRANSPORT": "stdio" }
    }
  }
}
```

```bat
claude mcp add --scope user --transport stdio knx-ets -e MCP_TRANSPORT=stdio -- "C:\Tools\knx-ets-mcp.exe"
```

#### C. Server on a different machine than ETS (server → ETS over TCP)

To run the MCP server on another machine than the ETS PC, the server reaches
ETS over the **ETS TCP endpoint** instead of the pipe. Enable it in the AddIn's
**Configuration / Preferences**: **Enable TCP endpoint**, **Port** (default `8730`),
**Allow LAN**; the panel shows the endpoint and the **token** (authentication is always on:
leave the field empty and a random token is generated; confirm the dialog, or type your
own, to keep the same token across ETS restarts). Then run the server from this repo pointing at it (if you don't have
the repo on that machine, install the release wheel once with
`uv tool install ./knx_ets_mcp-<version>-py3-none-any.whl` and run `knx-ets-mcp` instead):

```bat
set "MCP_TRANSPORT=http"
set "KNX_BRIDGE_TRANSPORT=tcp"
set "KNX_BRIDGE_HOST=<ets-ip>"
set "KNX_BRIDGE_PORT=8730"
set "KNX_BRIDGE_TOKEN=<token-from-the-panel>"
uv run --directory C:\path\to\knx-ets-mcp\mcp-server knx-ets-mcp
```

Set `KNX_BRIDGE_TOKEN` to the token shown in the AddIn panel (a missing or wrong token is
rejected with `auth_failed`). Neither the ETS TCP link nor the HTTP endpoint is
encrypted — trusted LAN only.

Restart (or reconnect) the AI client so it picks up the server.

### 5. Work with your assistant

To confirm that the AddIn is running and which version is loaded, call `bridge.info` --
it returns the AddIn build version, the ETS SDK version, and the current project
name/ID/revision. Your assistant can do this automatically.

Ask the assistant to inspect and build your installation, for example:

- "List the devices and group addresses in this project."
- "Create group address 1/1/5 named 'Kitchen light' and link it to the switch actuator's
  channel A."
- "Add an MDT switch actuator from the catalog to line 1.1, then create a room 'Kitchen'
  and assign it."

### 6. Programming and firmware

**Programming a device** (downloading its configuration — individual address, group
address table, parameters, application) runs **automatically** and needs no approval. It
is reversible: if something is wrong, program again with a corrected configuration. It
uses KNXnet/IP. You can request a partial download via options.

**Firmware updates** are different — they flash the device and can be irreversible, and in
ETS apply to **IoT devices only**. They are gated by a preference:

- By default, a firmware request returns `approval_required`.
- To allow it, enable **Unattended firmware update** in the AddIn's Configuration /
  Preferences — a standing authorization you set once by hand. Only enable it if you
  accept that firmware may be flashed without a further prompt.

**Bus read-only mode.** To let the assistant work on the project and *read* the bus
(scan, ping, group read, monitor) while making sure it cannot touch the installation,
enable **Bus read-only** in the AddIn's Configuration / Preferences (ETS 6) or panel
(ETS 5). Every bus write — programming, group writes, device reset/unload, individual
address, firmware — then returns `approval_required`. It is off by default.

Each tool also carries standard MCP safety annotations (`readOnlyHint`,
`destructiveHint`, `openWorldHint`), so clients that ask for confirmation on destructive
tools will prompt before programming, group writes, resets or deletions.

## Status

Early / experimental. Essentially the full practical ETS 5/6 SDK surface is exposed --
inspect, build, edit (delete/rename/topology/flags/moves), building view, trades, tags,
to-dos, project history, certificates, bus interface, segments, additional addresses,
catalog, project (export/undo-redo/navigate), programming, firmware, and bus operations
including project recovery. All covered by an automated test suite against an in-memory
mock. Final verification of bus operations against live ETS + hardware is ongoing. A few
project operations are intentionally **not supported** (open/list/create a project,
import `.knxproj`, device.move preserving config) because the SDK has no API for them.
Expect rough edges.

## For developers

Building the AddIn yourself (ETS6 and ETS5 targets, providing the SDK DLLs, packaging) is
documented in [BUILD.md](BUILD.md). The architecture, SDK details, and the AddIn↔MCP
protocol are in [CLAUDE.md](CLAUDE.md), [docs/protocol.md](docs/protocol.md), and
[addin/README.md](addin/README.md).
