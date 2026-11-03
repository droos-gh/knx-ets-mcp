using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnxEtsMcp.Tests.Support;

/// <summary>
/// JSON assertion helpers for the ported tests (set B). The "B" suffix keeps these
/// apart from the helpers in JsonTestExtensionsA.
/// </summary>
public static class JsonTestExtensionsB
{
    /// <summary>The node as a string (fails unless it is a JSON string).</summary>
    public static string StrB(this JsonNode? node)
    {
        AssertKindB(node, JsonValueKind.String);
        return node!.GetValue<string>();
    }

    /// <summary>The node as an int (fails unless it is a JSON number).</summary>
    public static int IntB(this JsonNode? node)
    {
        AssertKindB(node, JsonValueKind.Number);
        var number = decimal.Parse(node!.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        return checked((int)number);
    }

    /// <summary>Python <c>x is True</c>: the node must be the JSON literal true.</summary>
    public static void IsTrueB(this JsonNode? node)
    {
        Assert.NotNull(node);
        Assert.Equal(JsonValueKind.True, node.GetValueKind());
    }

    /// <summary>Python <c>x is False</c>: the node must be the JSON literal false.</summary>
    public static void IsFalseB(this JsonNode? node)
    {
        Assert.NotNull(node);
        Assert.Equal(JsonValueKind.False, node.GetValueKind());
    }

    /// <summary>Python <c>isinstance(x, bool)</c>.</summary>
    public static void IsBoolB(this JsonNode? node)
    {
        Assert.NotNull(node);
        var kind = node.GetValueKind();
        Assert.True(kind is JsonValueKind.True or JsonValueKind.False, $"Expected a bool but got {node.ToJsonString()}");
    }

    /// <summary>Fails unless the node is a non-null JSON value of <paramref name="kind"/>.</summary>
    public static void AssertKindB(JsonNode? node, JsonValueKind kind)
    {
        Assert.NotNull(node);
        Assert.Equal(kind, node.GetValueKind());
    }

    /// <summary>The node as an object.</summary>
    public static JsonObject ObjB(this JsonNode? node) => Assert.IsType<JsonObject>(node);

    /// <summary>The node as an array.</summary>
    public static JsonArray ArrB(this JsonNode? node) => Assert.IsType<JsonArray>(node);

    /// <summary>The node as an array of objects.</summary>
    public static List<JsonObject> ObjectsB(this JsonNode? node)
        => node.ArrB().Select(item => item.ObjB()).ToList();

    /// <summary>Python <c>key in obj</c> (true also when the value is JSON null).</summary>
    public static bool HasB(this JsonNode? node, string key)
        => node is JsonObject obj && obj.ContainsKey(key);

    /// <summary>The string at <paramref name="key"/>, or null when absent / not a string (Python <c>d.get(key)</c>).</summary>
    public static string? GetStrB(this JsonNode? node, string key)
        => node is JsonObject obj
           && obj[key] is JsonValue value
           && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;

    /// <summary>Python <c>next(x for x in items if x["ref"] == reference)</c>.</summary>
    public static JsonObject FindByRefB(this JsonNode? array, string reference)
    {
        var match = array.ObjectsB().FirstOrDefault(o => o.GetStrB("ref") == reference);
        Assert.NotNull(match);
        return match;
    }

    /// <summary>The string values of a JSON array.</summary>
    public static List<string> StringsB(this JsonNode? node)
        => node.ArrB().Select(item => item.StrB()).ToList();
}
