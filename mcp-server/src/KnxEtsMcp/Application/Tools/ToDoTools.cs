// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: ToDo tools.</summary>
[McpServerToolType]
public sealed class ToDoTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_list_todos", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List all to-do items in the KNX project.
        """)]
    public async Task<JsonNode?> ListTodos(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("todos.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_create_todo", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a to-do item in the KNX project.
        """)]
    public async Task<JsonNode?> CreateTodo(
        string description,
        string? object_path = null,
        string? status = null,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("description", description)
            .SetOptional("objectPath", object_path)
            .SetOptional("status", status);
        var result = await bridge.CallAsync("todo.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_todo", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a to-do item from the KNX project.
        """)]
    public async Task<JsonNode?> DeleteTodo(
        string todo_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("todoRef", todo_ref);
        var result = await bridge.CallAsync("todo.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
