using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.Application;

namespace KnxEtsMcp.Tests;

/// <summary>Environment-variable configuration (same variable names as the former Python server).</summary>
public sealed class ServerSettingsTests
{
    private static Dictionary<string, string?> Env(params (string Key, string Value)[] vars)
        => vars.ToDictionary(v => v.Key, v => (string?)v.Value, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Defaults_AreHttpOnAllInterfacesWithPipeBridge()
    {
        var settings = ServerSettings.FromEnvironment(Env()).Value;

        Assert.Equal(ClientTransport.Http, settings.ClientTransport);
        Assert.Equal("0.0.0.0", settings.Host);
        Assert.Equal(8765, settings.Port);
        Assert.Equal("/mcp", settings.Path);
        Assert.Equal("pipe", settings.BridgeTransport);
    }

    [Theory]
    [InlineData("stdio", ClientTransport.Stdio)]
    [InlineData("http", ClientTransport.Http)]
    [InlineData("streamable-http", ClientTransport.Http)]
    [InlineData("STDIO", ClientTransport.Stdio)]
    public void McpTransport_IsParsed(string value, ClientTransport expected)
    {
        Assert.Equal(expected, ServerSettings.FromEnvironment(Env(("MCP_TRANSPORT", value))).Value.ClientTransport);
    }

    [Fact]
    public void UnknownMcpTransport_IsRejected()
    {
        var result = ServerSettings.FromEnvironment(Env(("MCP_TRANSPORT", "carrier-pigeon")));

        Assert.False(result.IsSuccess);
        Assert.Contains("MCP_TRANSPORT", result.Error!.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("abc")]
    public void InvalidMcpPort_IsRejected(string port)
    {
        Assert.False(ServerSettings.FromEnvironment(Env(("MCP_PORT", port))).IsSuccess);
    }

    [Fact]
    public void Path_GetsALeadingSlash()
    {
        Assert.Equal("/knx", ServerSettings.FromEnvironment(Env(("MCP_PATH", "knx"))).Value.Path);
    }

    [Fact]
    public void TcpBridge_RequiresPort()
    {
        var result = ServerSettings.FromEnvironment(Env(("KNX_BRIDGE_TRANSPORT", "tcp"), ("KNX_BRIDGE_TOKEN", "t")));

        Assert.False(result.IsSuccess);
        Assert.Contains("KNX_BRIDGE_PORT", result.Error!.Message);
    }

    [Fact]
    public void TcpBridge_RequiresToken()
    {
        var result = ServerSettings.FromEnvironment(Env(("KNX_BRIDGE_TRANSPORT", "tcp"), ("KNX_BRIDGE_PORT", "8730")));

        Assert.False(result.IsSuccess);
        Assert.Contains("KNX_BRIDGE_TOKEN", result.Error!.Message);
    }

    [Fact]
    public void TcpBridge_WithPortAndToken_CreatesTcpTransport()
    {
        var settings = ServerSettings.FromEnvironment(Env(
            ("KNX_BRIDGE_TRANSPORT", "tcp"), ("KNX_BRIDGE_HOST", "10.0.0.5"),
            ("KNX_BRIDGE_PORT", "8730"), ("KNX_BRIDGE_TOKEN", "t"))).Value;

        Assert.Equal("10.0.0.5", settings.BridgeHost);
        Assert.Equal(8730, settings.BridgePort);
        Assert.IsType<TcpTransport>(settings.CreateBridgeTransport());
    }

    [Fact]
    public void MockBridge_CreatesMockTransport()
    {
        var settings = ServerSettings.FromEnvironment(Env(("KNX_BRIDGE_TRANSPORT", "mock"))).Value;

        Assert.IsType<MockTransport>(settings.CreateBridgeTransport());
    }

    [Fact]
    public void UnknownBridgeTransport_IsRejected()
    {
        Assert.False(ServerSettings.FromEnvironment(Env(("KNX_BRIDGE_TRANSPORT", "usb"))).IsSuccess);
    }
}
