using System.Reflection;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Application;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// Entry point: wires configuration, the AddIn bridge and the MCP server.
// Client-facing transport: MCP_TRANSPORT=http (default, Streamable HTTP on
// MCP_HOST:MCP_PORT MCP_PATH) or stdio. AddIn-facing transport: KNX_BRIDGE_TRANSPORT=
// pipe (default) | tcp | mock. See ServerSettings and HttpAuth.

var environment = HttpAuth.ProcessEnvironment();
var settingsResult = ServerSettings.FromEnvironment(environment);
if (!settingsResult.IsSuccess)
{
    Console.Error.WriteLine($"[knx-ets-mcp] {settingsResult.Error!.Message}");
    return 2;
}

var settings = settingsResult.Value;
var bridge = new KnxBridgeClient(settings.CreateBridgeTransport());
var version = typeof(ServerSettings).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

void ConfigureMcp(McpServerOptions options)
{
    options.ServerInfo = new Implementation { Name = "knx-ets-bridge", Version = version };
    options.ServerInstructions = ServerInstructions.Text;
}

if (settings.ClientTransport == ClientTransport.Stdio)
{
    var stdioBuilder = Host.CreateApplicationBuilder(args);
    // stdout carries the MCP protocol: every log line must go to stderr.
    stdioBuilder.Logging.ClearProviders();
    stdioBuilder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    stdioBuilder.Services.AddSingleton<IKnxBridge>(bridge);
    stdioBuilder.Services
        .AddMcpServer(ConfigureMcp)
        .WithStdioServerTransport()
        .AddKnxTools();
    await stdioBuilder.Build().RunAsync();
    return 0;
}

var auth = HttpAuth.Resolve(settings.Host, environment);
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.WebHost.UseUrls($"http://{UrlHost(settings.Host)}:{settings.Port}");
if (HttpAuth.IsLoopback(settings.Host))
{
    // Loopback without a token: only accept local Host headers (DNS-rebinding guard).
    builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1;[::1]";
}
builder.Services.AddSingleton<IKnxBridge>(bridge);
builder.Services
    .AddMcpServer(ConfigureMcp)
    .WithHttpTransport(_ => { })
    .AddKnxTools();

var app = builder.Build();
if (auth.Token is { } token)
{
    app.Use(async (context, next) =>
    {
        if (!HttpAuth.IsAuthorized(context.Request.Headers.Authorization.ToString(), token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }
        await next(context);
    });
}
app.MapMcp(settings.Path);

PrintBanner(settings, auth, environment);
await app.RunAsync();
return 0;

static string UrlHost(string host) => host.Contains(':') && !host.StartsWith('[') ? $"[{host}]" : host;

static void PrintBanner(ServerSettings settings, HttpAuthSettings auth, IReadOnlyDictionary<string, string?> environment)
{
    var url = $"http://{UrlHost(settings.Host)}:{settings.Port}{settings.Path}";
    Console.Error.WriteLine($"[knx-ets-mcp] Streamable HTTP on {url} (auth: {(auth.Enabled ? "on" : "OFF")} -- {auth.Source})");

    var explicitToken = environment.TryGetValue("MCP_AUTH_TOKEN", out var t) && !string.IsNullOrWhiteSpace(t);
    if (auth.Enabled && !explicitToken)
    {
        // Generated/persisted token: show it so the user can configure the client.
        var urlHost = settings.Host == HttpAuth.DefaultHost ? "<this-machine-ip>" : UrlHost(settings.Host);
        Console.Error.WriteLine($"[knx-ets-mcp] Bearer token: {auth.Token}");
        Console.Error.WriteLine("[knx-ets-mcp] Connect with:");
        Console.Error.WriteLine(
            $"  claude mcp add --scope user --transport http knx-ets http://{urlHost}:{settings.Port}{settings.Path} "
          + $"--header \"Authorization: Bearer {auth.Token}\"");
    }

    if (!auth.Enabled && !HttpAuth.IsLoopback(settings.Host))
    {
        Console.Error.WriteLine(
            "[knx-ets-mcp] WARNING: MCP_ALLOW_NO_AUTH is set and the server is bound to a network "
          + "interface -- anyone who can reach this port can control ETS and program devices. "
          + "Use only on a trusted, isolated LAN.");
    }
}
