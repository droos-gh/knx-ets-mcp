using KnxEtsMcp.Application;

namespace KnxEtsMcp.Tests;

/// <summary>Secure-by-default bearer-token auth on the client-facing HTTP transport.</summary>
public sealed class HttpAuthTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "knx-ets-mcp-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Per-test token file, so no test touches the real user profile.</summary>
    private string TokenFile => Path.Combine(_dir, "knx-ets-mcp", "http-token");

    private Dictionary<string, string?> Env(params (string Key, string Value)[] vars)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["MCP_AUTH_TOKEN_FILE"] = TokenFile };
        foreach (var (key, value) in vars)
            env[key] = value;
        return env;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void NetworkBindWithoutToken_GeneratesAndPersistsOne()
    {
        var settings = HttpAuth.Resolve("0.0.0.0", Env());

        Assert.True(settings.Enabled);
        Assert.True(settings.Token!.Length >= 32);
        Assert.Equal(TokenFile, settings.Source);
        Assert.Equal(settings.Token, File.ReadAllText(TokenFile).Trim());
    }

    [Fact]
    public void GeneratedToken_IsStableAcrossRestarts()
    {
        var first = HttpAuth.Resolve("0.0.0.0", Env()).Token;
        var second = HttpAuth.Resolve("192.168.1.10", Env()).Token;

        Assert.Equal(first, second);
    }

    [Fact]
    public void GeneratedTokenFile_IsOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
            return; // POSIX permission bits only.

        HttpAuth.Resolve("0.0.0.0", Env());

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(TokenFile));
    }

    [Fact]
    public void EmptyTokenFile_IsRegenerated()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TokenFile)!);
        File.WriteAllText(TokenFile, "  \n");

        var settings = HttpAuth.Resolve("0.0.0.0", Env());

        Assert.False(string.IsNullOrEmpty(settings.Token));
        Assert.Equal(settings.Token, File.ReadAllText(TokenFile).Trim());
    }

    [Fact]
    public void ExplicitToken_Wins()
    {
        var settings = HttpAuth.Resolve("0.0.0.0", Env(("MCP_AUTH_TOKEN", "  secret  ")));

        Assert.Equal("secret", settings.Token);
        Assert.Equal("MCP_AUTH_TOKEN", settings.Source);
        Assert.False(File.Exists(TokenFile));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("::1")]
    [InlineData("LOCALHOST")]
    public void LoopbackBind_NeedsNoToken(string host)
    {
        var settings = HttpAuth.Resolve(host, Env());

        Assert.False(settings.Enabled);
        Assert.False(File.Exists(TokenFile));
    }

    [Fact]
    public void ExplicitToken_AlsoAppliesToLoopback()
    {
        Assert.Equal("secret", HttpAuth.Resolve("127.0.0.1", Env(("MCP_AUTH_TOKEN", "secret"))).Token);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("YES")]
    [InlineData("on")]
    public void ExplicitOptOut_DisablesAuthOnNetworkBind(string value)
    {
        var settings = HttpAuth.Resolve("0.0.0.0", Env(("MCP_ALLOW_NO_AUTH", value)));

        Assert.False(settings.Enabled);
        Assert.Equal("MCP_ALLOW_NO_AUTH", settings.Source);
        Assert.False(File.Exists(TokenFile));
    }

    [Fact]
    public void OptOut_RequiresATruthyValue()
    {
        Assert.True(HttpAuth.Resolve("0.0.0.0", Env(("MCP_ALLOW_NO_AUTH", "0"))).Enabled);
    }

    [Theory]
    [InlineData("Bearer secret", true)]
    [InlineData("bearer secret", true)]
    [InlineData("Bearer  secret ", true)]
    [InlineData("Bearer wrong", false)]
    [InlineData("Bearer secre", false)]
    [InlineData("secret", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAuthorized_AcceptsOnlyTheBearerToken(string? header, bool expected)
    {
        Assert.Equal(expected, HttpAuth.IsAuthorized(header, "secret"));
    }
}
