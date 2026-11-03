using System.Globalization;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.BusinessLogic;

namespace KnxEtsMcp.Application;

/// <summary>How the AI client reaches this server.</summary>
public enum ClientTransport
{
    /// <summary>Streamable HTTP endpoint (default).</summary>
    Http,

    /// <summary>The AI client launches the exe and talks over stdin/stdout.</summary>
    Stdio,
}

/// <summary>
/// Settings read from environment variables (same names as the former Python server):
/// MCP_TRANSPORT, MCP_HOST, MCP_PORT, MCP_PATH (client side) and KNX_BRIDGE_TRANSPORT,
/// KNX_BRIDGE_HOST, KNX_BRIDGE_PORT, KNX_BRIDGE_TOKEN (AddIn side).
/// </summary>
public sealed record ServerSettings(
    ClientTransport ClientTransport,
    string Host,
    int Port,
    string Path,
    string BridgeTransport,
    string BridgeHost,
    int BridgePort,
    string BridgeToken)
{
    public static Result<ServerSettings> FromEnvironment(IReadOnlyDictionary<string, string?> environment)
    {
        string Get(string name, string fallback)
            => environment.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

        var clientTransport = Get("MCP_TRANSPORT", "http").ToLowerInvariant() switch
        {
            "http" or "streamable-http" => (ClientTransport?)ClientTransport.Http,
            "stdio" => ClientTransport.Stdio,
            _ => null,
        };
        if (clientTransport is null)
            return Fail($"MCP_TRANSPORT must be 'http' or 'stdio' (got '{Get("MCP_TRANSPORT", "")}')");

        if (!TryPort(Get("MCP_PORT", "8765"), out var port))
            return Fail($"MCP_PORT is not a valid port: '{Get("MCP_PORT", "")}'");

        var path = Get("MCP_PATH", "/mcp");
        if (!path.StartsWith('/'))
            path = "/" + path;

        var bridgeTransport = Get("KNX_BRIDGE_TRANSPORT", "pipe").ToLowerInvariant();
        var bridgePort = 0;
        var bridgeToken = Get("KNX_BRIDGE_TOKEN", "");
        switch (bridgeTransport)
        {
            case "pipe" or "mock":
                break;
            case "tcp":
                var rawPort = Get("KNX_BRIDGE_PORT", "");
                if (rawPort.Length == 0)
                    return Fail("KNX_BRIDGE_PORT is required when KNX_BRIDGE_TRANSPORT=tcp");
                if (!TryPort(rawPort, out bridgePort))
                    return Fail($"KNX_BRIDGE_PORT is not a valid port: '{rawPort}'");
                // The AddIn always requires its token (shown in its panel): fail fast here
                // instead of an auth_failed on every call.
                if (bridgeToken.Length == 0)
                    return Fail("KNX_BRIDGE_TOKEN is required when KNX_BRIDGE_TRANSPORT=tcp "
                              + "(copy the token shown in the ETS AddIn panel)");
                break;
            default:
                return Fail($"KNX_BRIDGE_TRANSPORT must be 'pipe', 'tcp' or 'mock' (got '{bridgeTransport}')");
        }

        return Result<ServerSettings>.Ok(new ServerSettings(
            clientTransport.Value,
            Get("MCP_HOST", HttpAuth.DefaultHost),
            port,
            path,
            bridgeTransport,
            Get("KNX_BRIDGE_HOST", "127.0.0.1"),
            bridgePort,
            bridgeToken));
    }

    /// <summary>The AddIn-facing transport selected by KNX_BRIDGE_TRANSPORT.</summary>
    public ITransport CreateBridgeTransport() => BridgeTransport switch
    {
        "mock" => new MockTransport(),
        "tcp" => new TcpTransport(BridgeHost, BridgePort, BridgeToken),
        _ => new NamedPipeTransport(),
    };

    private static bool TryPort(string raw, out int port)
        => int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535;

    private static Result<ServerSettings> Fail(string message) => Result<ServerSettings>.Fail("invalid_config", message);
}
