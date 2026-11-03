using System.Security.Cryptography;
using System.Text;

namespace KnxEtsMcp.Application;

/// <summary>Outcome of the token resolution for the HTTP endpoint.</summary>
/// <param name="Token">The bearer token clients must send, or null when auth is off.</param>
/// <param name="Source">Where the decision came from, printed at startup.</param>
public sealed record HttpAuthSettings(string? Token, string Source)
{
    public bool Enabled => Token is not null;
}

/// <summary>
/// Bearer-token auth for the client-facing Streamable HTTP endpoint. Secure by default:
/// a network bind (the default MCP_HOST=0.0.0.0) ALWAYS requires a token.
/// Resolution, first match wins:
/// <list type="number">
/// <item>MCP_AUTH_TOKEN -- an explicit token;</item>
/// <item>loopback bind (127.0.0.1 / localhost / ::1) -- no token (host filtering guards
/// against DNS rebinding);</item>
/// <item>MCP_ALLOW_NO_AUTH=1 -- explicit opt-out (trusted LAN only);</item>
/// <item>otherwise a random token generated ONCE and kept in a per-user file
/// (MCP_AUTH_TOKEN_FILE, default %LOCALAPPDATA%\knx-ets-mcp\http-token), so the client
/// configuration stays valid across restarts.</item>
/// </list>
/// Independent of the AddIn token (KNX_BRIDGE_TOKEN), which protects the server-to-AddIn link.
/// </summary>
public static class HttpAuth
{
    public const string DefaultHost = "0.0.0.0";

    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "127.0.0.1", "localhost", "::1",
    };

    private static readonly HashSet<string> Truthy = new(StringComparer.OrdinalIgnoreCase)
    {
        "1", "true", "yes", "on",
    };

    public static bool IsLoopback(string host) => LoopbackHosts.Contains(host.Trim());

    /// <summary>Resolves the auth settings for a server bound to <paramref name="host"/>.</summary>
    /// <param name="environment">Environment variables (injectable for tests).</param>
    public static HttpAuthSettings Resolve(string host, IReadOnlyDictionary<string, string?> environment)
    {
        var explicitToken = Get(environment, "MCP_AUTH_TOKEN").Trim();
        if (explicitToken.Length > 0)
            return new HttpAuthSettings(explicitToken, "MCP_AUTH_TOKEN");
        if (IsLoopback(host))
            return new HttpAuthSettings(null, "loopback bind");
        if (Truthy.Contains(Get(environment, "MCP_ALLOW_NO_AUTH").Trim()))
            return new HttpAuthSettings(null, "MCP_ALLOW_NO_AUTH");

        var path = TokenFilePath(environment);
        return new HttpAuthSettings(LoadOrCreateToken(path), path);
    }

    public static string TokenFilePath(IReadOnlyDictionary<string, string?> environment)
    {
        var overridePath = Get(environment, "MCP_AUTH_TOKEN_FILE").Trim();
        if (overridePath.Length > 0)
            return overridePath;

        var root = Get(environment, "LOCALAPPDATA");
        if (root.Length == 0)
            root = Get(environment, "XDG_CONFIG_HOME");
        if (root.Length == 0)
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(root, "knx-ets-mcp", "http-token");
    }

    /// <summary>
    /// Returns the token stored at <paramref name="path"/>, creating a random one if the
    /// file is missing or empty. On Unix the file is created owner-only (0600); on Windows
    /// the per-user %LOCALAPPDATA% folder already restricts access.
    /// </summary>
    public static string LoadOrCreateToken(string path)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (existing.Length > 0)
                return existing;
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var stream = new FileStream(path, options))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(token + "\n");
        }
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return token;
    }

    /// <summary>Constant-time check of an "Authorization: Bearer &lt;token&gt;" header value.</summary>
    public static bool IsAuthorized(string? authorizationHeader, string expectedToken)
    {
        const string prefix = "Bearer ";
        if (authorizationHeader is null || !authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var presented = Encoding.UTF8.GetBytes(authorizationHeader[prefix.Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(expectedToken);
        return CryptographicOperations.FixedTimeEquals(presented, expected);
    }

    /// <summary>Snapshot of the process environment, for <see cref="Resolve"/>.</summary>
    public static IReadOnlyDictionary<string, string?> ProcessEnvironment()
    {
        // Case-insensitive like Windows; on Unix a later duplicate (e.g. Path/PATH) just wins.
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            environment[(string)entry.Key] = (string?)entry.Value;
        return environment;
    }

    private static string Get(IReadOnlyDictionary<string, string?> environment, string name)
        => environment.TryGetValue(name, out var value) && value is not null ? value : "";

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
