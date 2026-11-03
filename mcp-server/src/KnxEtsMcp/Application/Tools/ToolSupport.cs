using System.Text.Json;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol;

namespace KnxEtsMcp.Application.Tools;

/// <summary>Glue between the MCP tool methods and <see cref="IKnxBridge"/>.</summary>
internal static class BridgeCalls
{
    /// <summary>
    /// Calls the AddIn and returns its result. A protocol error becomes an MCP tool error
    /// whose text is "[code] message" -- the application boundary translates the
    /// Result failure into the form the LLM sees (McpException is how the MCP SDK
    /// reports a failed tool call).
    /// </summary>
    public static async Task<JsonNode?> CallAsync(
        this IKnxBridge bridge, string method, JsonObject? parameters, string? expectedRevision,
        CancellationToken cancellationToken)
    {
        var result = await bridge.RequestAsync(method, parameters, expectedRevision, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            throw new McpException(result.Error!.ToString());
        return result.Value;
    }
}

/// <summary>
/// Fluent protocol-parameter builder. <c>Set</c> always writes the key (null becomes JSON
/// null), <c>SetOptional</c> skips null values, <c>SetFlag</c> writes <c>true</c> only when set
/// -- the three shapes the protocol uses (docs/protocol.md).
/// </summary>
internal static class ProtocolParams
{
    public static JsonObject Set(this JsonObject o, string key, string? value) { o[key] = JsonValue.Create(value); return o; }

    public static JsonObject Set(this JsonObject o, string key, int? value) { o[key] = JsonValue.Create(value); return o; }

    public static JsonObject Set(this JsonObject o, string key, bool? value) { o[key] = JsonValue.Create(value); return o; }

    public static JsonObject Set(this JsonObject o, string key, double? value) { o[key] = JsonValue.Create(value); return o; }

    public static JsonObject Set(this JsonObject o, string key, IEnumerable<string>? values)
    {
        o[key] = values is null ? null : new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
        return o;
    }

    public static JsonObject Set(this JsonObject o, string key, JsonElement value)
    {
        o[key] = JsonNode.Parse(value.GetRawText());
        return o;
    }

    public static JsonObject SetOptional(this JsonObject o, string key, string? value)
        => value is null ? o : o.Set(key, value);

    public static JsonObject SetOptional(this JsonObject o, string key, int? value)
        => value is null ? o : o.Set(key, value);

    public static JsonObject SetOptional(this JsonObject o, string key, bool? value)
        => value is null ? o : o.Set(key, value);

    public static JsonObject SetOptional(this JsonObject o, string key, double? value)
        => value is null ? o : o.Set(key, value);

    public static JsonObject SetOptional(this JsonObject o, string key, IEnumerable<string>? values)
        => values is null ? o : o.Set(key, values);

    public static JsonObject SetOptional(this JsonObject o, string key, JsonElement? value)
        => value is null ? o : o.Set(key, value.Value);

    public static JsonObject SetFlag(this JsonObject o, string key, bool flag)
        => flag ? o.Set(key, true) : o;
}

/// <summary>Client-side scoping of list results (name_contains, active_only).</summary>
internal static class JsonFilters
{
    /// <summary>
    /// Keeps the items where any of <paramref name="fields"/> contains
    /// <paramref name="needle"/> (case-insensitive). No-op when the needle is empty or the
    /// result is not an array. Missing/null/falsy field values count as "".
    /// </summary>
    public static JsonNode? WhereAnyFieldContains(JsonNode? result, string? needle, params string[] fields)
    {
        if (string.IsNullOrEmpty(needle) || result is not JsonArray items)
            return result;
        var lowered = needle.ToLowerInvariant();
        return Filter(items, item => fields.Any(f => FieldText(item, f).Contains(lowered, StringComparison.Ordinal)));
    }

    /// <summary>Keeps the items whose <paramref name="field"/> is truthy (e.g. isActive).</summary>
    public static JsonNode? WhereTruthy(JsonNode? result, string field)
        => result is JsonArray items ? Filter(items, item => IsTruthy(item[field])) : result;

    private static JsonArray Filter(JsonArray items, Func<JsonObject, bool> keep)
        => new(items.OfType<JsonObject>().Where(keep).Select(i => (JsonNode?)i.DeepClone()).ToArray());

    private static string FieldText(JsonObject item, string field)
    {
        var node = item[field];
        if (!IsTruthy(node))
            return "";
        if (node is JsonValue value && value.TryGetValue<string>(out var s))
            return s.ToLowerInvariant();
        return node!.ToJsonString().ToLowerInvariant();
    }

    private static bool IsTruthy(JsonNode? node)
    {
        if (node is null)
            return false;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b))
                return b;
            if (v.TryGetValue<string>(out var s))
                return s.Length > 0;
            if (v.TryGetValue<double>(out var d))
                return d != 0;
            return true;
        }
        if (node is JsonArray a)
            return a.Count > 0;
        return node is not JsonObject o || o.Count > 0;
    }
}
