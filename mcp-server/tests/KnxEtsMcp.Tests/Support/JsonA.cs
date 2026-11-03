using System.Text.Json.Nodes;

namespace KnxEtsMcp.Tests.Support;

/// <summary>
/// Reading helpers for tool/protocol JSON in tests. Each accessor mirrors Python's
/// <c>data["key"]</c>: it fails the test when the key is missing (Python KeyError).
/// Method names are deliberately distinct (…At) so they never clash with other
/// extension classes in this namespace.
/// </summary>
public static class JsonTestExtensionsA
{
    /// <summary>The node as an object (fails the test otherwise).</summary>
    public static JsonObject AsObj(this JsonNode? node) => Assert.IsType<JsonObject>(node);

    /// <summary>Python <c>key in data</c>.</summary>
    public static bool HasProp(this JsonNode? node, string key) => node.AsObj().ContainsKey(key);

    /// <summary>Python <c>data[key]</c>: the raw value (null for a JSON null); fails when the key is missing.</summary>
    public static JsonNode? NodeAt(this JsonNode? node, string key)
    {
        var obj = node.AsObj();
        Assert.True(obj.ContainsKey(key), $"Missing key '{key}' in {obj.ToJsonString()}");
        return obj[key];
    }

    /// <summary>A string property (null for a JSON null); fails when the key is missing.</summary>
    public static string? StringAt(this JsonNode? node, string key)
        => node.NodeAt(key)?.GetValue<string>();

    /// <summary>Python <c>data.get(key)</c> for a string: null when missing or JSON null.</summary>
    public static string? OptStringAt(this JsonNode? node, string key)
        => node.AsObj()[key]?.GetValue<string>();

    public static int IntAt(this JsonNode? node, string key)
    {
        var value = node.NodeAt(key);
        Assert.NotNull(value);
        return value.GetValue<int>();
    }

    public static bool BoolAt(this JsonNode? node, string key)
    {
        var value = node.NodeAt(key);
        Assert.NotNull(value);
        return value.GetValue<bool>();
    }

    public static JsonArray ArrayAt(this JsonNode? node, string key)
        => Assert.IsType<JsonArray>(node.NodeAt(key));

    public static JsonObject ObjectAt(this JsonNode? node, string key)
        => Assert.IsType<JsonObject>(node.NodeAt(key));

    /// <summary>The array elements as objects.</summary>
    public static List<JsonObject> Objects(this JsonArray array)
        => array.Select(n => n.AsObj()).ToList();

    /// <summary>The array elements as strings (e.g. a com-object's links).</summary>
    public static List<string> Strings(this JsonArray array)
        => array.Select(n =>
        {
            Assert.NotNull(n);
            return n.GetValue<string>();
        }).ToList();

    /// <summary>Python <c>{x[key] for x in array}</c> as a list.</summary>
    public static List<string?> StringsAt(this JsonArray array, string key)
        => array.Select(n => n.StringAt(key)).ToList();

    /// <summary>Python <c>next(x for x in array if x[key] == value)</c>; fails when absent.</summary>
    public static JsonObject FindBy(this JsonArray array, string key, string value)
    {
        var found = array.TryFindBy(key, value);
        Assert.NotNull(found);
        return found;
    }

    /// <summary>Python <c>next((x for x in array if x[key] == value), None)</c>.</summary>
    public static JsonObject? TryFindBy(this JsonArray array, string key, string value)
        => array.Objects().FirstOrDefault(o => o.OptStringAt(key) == value);
}
