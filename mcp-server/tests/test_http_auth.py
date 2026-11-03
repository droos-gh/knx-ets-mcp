"""Tests for the secure-by-default bearer-token auth on the client-facing HTTP transport."""

from __future__ import annotations

import os
import stat
import sys

import pytest
from fastmcp.server.auth.providers.jwt import StaticTokenVerifier

from knx_ets_mcp import http_auth
from knx_ets_mcp.server import _build_http_auth


@pytest.fixture(autouse=True)
def _isolated_env(monkeypatch, tmp_path):
    """Clean auth env + a per-test token file, so no test touches the real home dir."""
    for var in ("MCP_AUTH_TOKEN", "MCP_ALLOW_NO_AUTH", "MCP_HOST", "MCP_TRANSPORT"):
        monkeypatch.delenv(var, raising=False)
    token_file = tmp_path / "knx-ets-mcp" / "http-token"
    monkeypatch.setenv("MCP_AUTH_TOKEN_FILE", str(token_file))
    return token_file


# -- resolve_http_auth --------------------------------------------------------


def test_network_bind_without_token_generates_and_persists_one(_isolated_env) -> None:
    settings = http_auth.resolve_http_auth("0.0.0.0")
    assert settings.enabled
    assert len(settings.token) >= 32
    assert settings.source == str(_isolated_env)
    assert _isolated_env.read_text(encoding="utf-8").strip() == settings.token


def test_generated_token_is_stable_across_restarts(_isolated_env) -> None:
    first = http_auth.resolve_http_auth("0.0.0.0").token
    second = http_auth.resolve_http_auth("192.168.1.10").token
    assert first == second


@pytest.mark.skipif(sys.platform == "win32", reason="POSIX permission bits")
def test_generated_token_file_is_owner_only(_isolated_env) -> None:
    http_auth.resolve_http_auth("0.0.0.0")
    mode = stat.S_IMODE(os.stat(_isolated_env).st_mode)
    assert mode == 0o600


def test_empty_token_file_is_regenerated(_isolated_env) -> None:
    _isolated_env.parent.mkdir(parents=True)
    _isolated_env.write_text("  \n", encoding="utf-8")
    settings = http_auth.resolve_http_auth("0.0.0.0")
    assert settings.token
    assert _isolated_env.read_text(encoding="utf-8").strip() == settings.token


def test_explicit_token_wins(monkeypatch, _isolated_env) -> None:
    monkeypatch.setenv("MCP_AUTH_TOKEN", "  secret  ")
    settings = http_auth.resolve_http_auth("0.0.0.0")
    assert settings.token == "secret"
    assert settings.source == "MCP_AUTH_TOKEN"
    assert not _isolated_env.exists()


@pytest.mark.parametrize("host", ["127.0.0.1", "localhost", "::1", "LOCALHOST"])
def test_loopback_bind_needs_no_token(host, _isolated_env) -> None:
    settings = http_auth.resolve_http_auth(host)
    assert not settings.enabled
    assert not _isolated_env.exists()


def test_explicit_token_also_applies_to_loopback(monkeypatch) -> None:
    monkeypatch.setenv("MCP_AUTH_TOKEN", "secret")
    assert http_auth.resolve_http_auth("127.0.0.1").token == "secret"


@pytest.mark.parametrize("value", ["1", "true", "YES", "on"])
def test_explicit_opt_out_disables_auth_on_network_bind(monkeypatch, value, _isolated_env) -> None:
    monkeypatch.setenv("MCP_ALLOW_NO_AUTH", value)
    settings = http_auth.resolve_http_auth("0.0.0.0")
    assert not settings.enabled
    assert settings.source == "MCP_ALLOW_NO_AUTH"
    assert not _isolated_env.exists()


def test_opt_out_requires_a_truthy_value(monkeypatch) -> None:
    monkeypatch.setenv("MCP_ALLOW_NO_AUTH", "0")
    assert http_auth.resolve_http_auth("0.0.0.0").enabled


# -- _build_http_auth (server wiring) -----------------------------------------


def test_default_http_run_is_authenticated() -> None:
    # Defaults: MCP_TRANSPORT=http, MCP_HOST=0.0.0.0 -> a token is required.
    assert isinstance(_build_http_auth(), StaticTokenVerifier)


def test_no_auth_for_stdio_even_with_token(monkeypatch) -> None:
    # Auth is an HTTP concept; stdio must never require a token.
    monkeypatch.setenv("MCP_AUTH_TOKEN", "secret")
    monkeypatch.setenv("MCP_TRANSPORT", "stdio")
    assert _build_http_auth() is None


def test_loopback_http_run_has_no_auth(monkeypatch) -> None:
    monkeypatch.setenv("MCP_HOST", "127.0.0.1")
    assert _build_http_auth() is None


async def test_verifier_accepts_only_the_token(monkeypatch) -> None:
    monkeypatch.setenv("MCP_AUTH_TOKEN", "secret")
    verifier = _build_http_auth()
    assert await verifier.verify_token("secret") is not None
    assert await verifier.verify_token("wrong") is None
