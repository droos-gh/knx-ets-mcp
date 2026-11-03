using System.IO.Pipelines;
using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.Application;
using KnxEtsMcp.BusinessLogic.Gateways;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using CallToolResult = ModelContextProtocol.Protocol.CallToolResult;
using ITransport = KnxEtsMcp.Adapters.Transport.ITransport;
using StreamClientTransport = ModelContextProtocol.Protocol.StreamClientTransport;
using TextContentBlock = ModelContextProtocol.Protocol.TextContentBlock;

namespace KnxEtsMcp.Tests.Support;

/// <summary>
/// The full MCP tool layer, in memory: the real server (same tool registration as
/// Program.cs) runs over in-process pipes, and an <see cref="McpClient"/> talks to it.
/// The AddIn is a <see cref="MockTransport"/> by default.
/// Equivalent of the Python conftest `client` fixture.
/// </summary>
public sealed class McpTestHost : IAsyncDisposable
{
    private readonly IHost _host;

    private McpTestHost(IHost host, McpClient client, ITransport transport)
    {
        _host = host;
        Client = client;
        Transport = transport;
    }

    public McpClient Client { get; }

    /// <summary>The AddIn-facing transport behind the server.</summary>
    public ITransport Transport { get; }

    /// <summary>The mock AddIn, when the host was started with one (the default).</summary>
    public MockTransport Mock => Transport as MockTransport
        ?? throw new InvalidOperationException("This host does not use a MockTransport.");

    /// <summary>Starts a server whose AddIn is <paramref name="transport"/> (default: a fresh MockTransport).</summary>
    public static async Task<McpTestHost> StartAsync(ITransport? transport = null)
    {
        transport ??= new MockTransport();

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IKnxBridge>(new KnxBridgeClient(transport));
        builder.Services
            .AddMcpServer(o => o.ServerInstructions = ServerInstructions.Text)
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .AddKnxTools();

        var host = builder.Build();
        await host.StartAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));

        return new McpTestHost(host, client, transport);
    }

    /// <summary>Calls a tool; protocol errors come back as IsError results, not exceptions.</summary>
    public Task<CallToolResult> CallAsync(string tool, IReadOnlyDictionary<string, object?>? arguments = null)
        => Client.CallToolAsync(tool, arguments ?? new Dictionary<string, object?>()).AsTask();

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }
}

/// <summary>Assertion helpers equivalent to the Python conftest tool_data / error_text.</summary>
public static class ToolAssert
{
    /// <summary>The JSON result of a successful tool call (fails the test on an error result).</summary>
    public static JsonNode? Data(CallToolResult result)
    {
        Assert.False(result.IsError == true, $"Expected success but got error: {Text(result)}");
        return JsonNode.Parse(Text(result));
    }

    /// <summary>Data(...) as an object.</summary>
    public static JsonObject Object(CallToolResult result)
        => Assert.IsType<JsonObject>(Data(result));

    /// <summary>Data(...) as an array.</summary>
    public static JsonArray Array(CallToolResult result)
        => Assert.IsType<JsonArray>(Data(result));

    /// <summary>The error message of a failed tool call, e.g. "[not_found] Device x not found".</summary>
    public static string ErrorText(CallToolResult result)
    {
        Assert.True(result.IsError == true, $"Expected error but got success: {Text(result)}");
        return Text(result);
    }

    /// <summary>
    /// For calls the MCP layer itself rejects (missing/wrong-typed arguments): returns the
    /// failure message whether the SDK reports it as an IsError result or throws.
    /// </summary>
    public static async Task<string> FailureAsync(Func<Task<CallToolResult>> call)
    {
        try
        {
            return ErrorText(await call());
        }
        catch (Exception ex) when (ex is ModelContextProtocol.McpException)
        {
            return ex.Message;
        }
    }

    private static string Text(CallToolResult result)
        => result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
}

/// <summary>Short builder for tool arguments: <c>Args.Of(("device_ref", "dev-0001"))</c>.</summary>
public static class Args
{
    public static IReadOnlyDictionary<string, object?> Of(params (string Name, object? Value)[] arguments)
        => arguments.ToDictionary(a => a.Name, a => a.Value);
}
