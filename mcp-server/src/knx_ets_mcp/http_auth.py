"""Bearer-token auth for the client-facing Streamable HTTP endpoint.

Secure by default: when the server is bound to a network interface (the default
``MCP_HOST=0.0.0.0``) it ALWAYS requires ``Authorization: Bearer <token>``.

Token resolution (first match wins):

1. ``MCP_AUTH_TOKEN`` -- an explicit token.
2. Loopback bind (``127.0.0.1`` / ``localhost`` / ``::1``) -- no token. FastMCP's
   host/origin protection guards loopback binds against DNS rebinding.
3. ``MCP_ALLOW_NO_AUTH=1`` -- explicit, documented opt-out (trusted LAN only).
4. Otherwise a random token is generated ONCE and persisted in a per-user file
   (``MCP_AUTH_TOKEN_FILE``, default ``%LOCALAPPDATA%\\knx-ets-mcp\\http-token`` on
   Windows, ``$XDG_CONFIG_HOME/knx-ets-mcp/http-token`` elsewhere), so the client
   configuration stays valid across restarts.

This is independent of the ETS AddIn token (KNX_BRIDGE_TOKEN), which protects the
server <-> AddIn link.
"""

from __future__ import annotations

import os
import secrets
from dataclasses import dataclass
from pathlib import Path
from typing import Any

LOOPBACK_HOSTS = frozenset({"127.0.0.1", "localhost", "::1"})
DEFAULT_HOST = "0.0.0.0"

_TRUTHY = frozenset({"1", "true", "yes", "on"})


@dataclass(frozen=True)
class HttpAuthSettings:
    """Outcome of token resolution for the HTTP endpoint."""

    token: str | None
    # Human-readable origin of the decision, printed at startup.
    source: str

    @property
    def enabled(self) -> bool:
        return self.token is not None


def is_loopback(host: str) -> bool:
    return host.strip().lower() in LOOPBACK_HOSTS


def default_token_file() -> Path:
    override = os.environ.get("MCP_AUTH_TOKEN_FILE", "").strip()
    if override:
        return Path(override)
    base = os.environ.get("LOCALAPPDATA") or os.environ.get("XDG_CONFIG_HOME")
    root = Path(base) if base else Path.home() / ".config"
    return root / "knx-ets-mcp" / "http-token"


def load_or_create_token(path: Path) -> str:
    """Return the token stored at ``path``, creating a random one if missing/empty.

    The file is created with owner-only permissions (0600 on POSIX; on Windows the
    per-user %LOCALAPPDATA% directory already restricts access).
    """
    try:
        existing = path.read_text(encoding="utf-8").strip()
    except FileNotFoundError:
        existing = ""
    if existing:
        return existing

    token = secrets.token_urlsafe(32)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(token + "\n")
    return token


def resolve_http_auth(host: str) -> HttpAuthSettings:
    explicit = os.environ.get("MCP_AUTH_TOKEN", "").strip()
    if explicit:
        return HttpAuthSettings(explicit, "MCP_AUTH_TOKEN")
    if is_loopback(host):
        return HttpAuthSettings(None, "loopback bind")
    if os.environ.get("MCP_ALLOW_NO_AUTH", "").strip().lower() in _TRUTHY:
        return HttpAuthSettings(None, "MCP_ALLOW_NO_AUTH")
    path = default_token_file()
    return HttpAuthSettings(load_or_create_token(path), str(path))


def build_verifier(settings: HttpAuthSettings) -> Any:
    """FastMCP auth provider for ``settings``, or None when auth is off."""
    if settings.token is None:
        return None
    from fastmcp.server.auth.providers.jwt import StaticTokenVerifier

    return StaticTokenVerifier(tokens={settings.token: {"client_id": "knx-ets", "scopes": []}})
