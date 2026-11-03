using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

public sealed partial class MockTransport
{
    /// <summary>
    /// Python value semantics over <see cref="JsonNode"/>s, so the handlers can be ported
    /// line by line from mock.py: truthiness, <c>==</c>, <c>str()</c>, dict-key hashing,
    /// iteration, <c>int()</c> parsing and <c>f"{x:03d}"</c>.
    /// Where Python would raise an uncaught TypeError/ValueError/AttributeError (a crash of
    /// the Python mock, not a protocol error), <see cref="Crash"/> throws an
    /// <see cref="InvalidOperationException"/> that escapes <c>SendAsync</c>, like the Python
    /// exception escapes <c>send()</c>.
    /// </summary>
    private static class Py
    {
        /// <summary>An uncaught Python exception (TypeError, ValueError, AttributeError, ...).</summary>
        public static InvalidOperationException Crash(string pythonError)
            => new($"Python mock would raise: {pythonError}");

        /// <summary>The JSON kind; C# null (missing key or JSON null) is <see cref="JsonValueKind.Null"/>.</summary>
        public static JsonValueKind Kind(JsonNode? n) => n is null ? JsonValueKind.Null : n.GetValueKind();

        /// <summary>Python <c>x is None</c>.</summary>
        public static bool IsNone(JsonNode? n) => Kind(n) is JsonValueKind.Null or JsonValueKind.Undefined;

        /// <summary>The Python type name, for crash messages.</summary>
        public static string TypeName(JsonNode? n) => Kind(n) switch
        {
            JsonValueKind.String => "str",
            JsonValueKind.True or JsonValueKind.False => "bool",
            JsonValueKind.Number => Number(n!).IsInt ? "int" : "float",
            JsonValueKind.Array => "list",
            JsonValueKind.Object => "dict",
            _ => "NoneType",
        };

        /// <summary>The string value when <paramref name="n"/> is a JSON string, else null.</summary>
        public static string? AsStringOrNull(JsonNode? n)
        {
            if (n is not JsonValue v)
                return null;
            if (v.TryGetValue<string>(out var s))
                return s;
            if (v.GetValueKind() == JsonValueKind.String)
            {
                // A string-kinded value backed by another CLR type (char, Guid, ...).
                using var doc = JsonDocument.Parse(v.ToJsonString());
                return doc.RootElement.GetString();
            }
            return null;
        }

        /// <summary>
        /// The string value; crashes like Python calling a str method on a non-str
        /// (e.g. <c>query.lower()</c> on an int).
        /// </summary>
        public static string RequireStr(JsonNode? n, string operation)
            => AsStringOrNull(n) ?? throw Crash($"'{TypeName(n)}' object has no attribute or support for '{operation}'");

        /// <summary>A Python number: int (arbitrary precision is narrowed to long) or float.</summary>
        public readonly record struct PyNumber(bool IsInt, long Int, double Float)
        {
            public double AsDouble => IsInt ? Int : Float;
        }

        /// <summary>Reads a JSON number as a Python int (no fraction/exponent) or float.</summary>
        public static PyNumber Number(JsonNode n)
        {
            var raw = n.ToJsonString();
            // A CLR double/float/decimal is a Python float even when it prints integral.
            var clrFloat = n is JsonValue v && v.TryGetValue<object>(out var boxed)
                && boxed is double or float or decimal or Half;
            var looksFloat = clrFloat || raw.AsSpan().IndexOfAny('.', 'e', 'E') >= 0;
            if (!looksFloat && long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
                return new PyNumber(true, l, l);
            return new PyNumber(false, 0, double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        /// <summary>Python truthiness (<c>if x:</c> / <c>not x</c>).</summary>
        public static bool Truthy(JsonNode? n)
        {
            switch (Kind(n))
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.String:
                    return AsStringOrNull(n)!.Length > 0;
                case JsonValueKind.Number:
                    var num = Number(n!);
                    return num.IsInt ? num.Int != 0 : num.Float != 0.0;
                case JsonValueKind.Array:
                    return n!.AsArray().Count > 0;
                case JsonValueKind.Object:
                    return n!.AsObject().Count > 0;
                default:
                    return true;
            }
        }

        private static bool IsNumeric(JsonValueKind k)
            => k is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;

        private static PyNumber NumericOf(JsonNode n) => Kind(n) switch
        {
            JsonValueKind.True => new PyNumber(true, 1, 1),
            JsonValueKind.False => new PyNumber(true, 0, 0),
            _ => Number(n),
        };

        /// <summary>Python <c>a == b</c> (bool == int, int == float, deep list/dict equality).</summary>
        public static bool Eq(JsonNode? a, JsonNode? b)
        {
            if (IsNone(a) || IsNone(b))
                return IsNone(a) && IsNone(b);
            var ka = Kind(a);
            var kb = Kind(b);
            if (IsNumeric(ka) && IsNumeric(kb))
            {
                var na = NumericOf(a!);
                var nb = NumericOf(b!);
                return na.IsInt && nb.IsInt ? na.Int == nb.Int : na.AsDouble == nb.AsDouble;
            }
            if (ka != kb)
                return false;
            switch (ka)
            {
                case JsonValueKind.String:
                    return string.Equals(AsStringOrNull(a), AsStringOrNull(b), StringComparison.Ordinal);
                case JsonValueKind.Array:
                {
                    var xa = a!.AsArray();
                    var xb = b!.AsArray();
                    if (xa.Count != xb.Count)
                        return false;
                    for (var i = 0; i < xa.Count; i++)
                    {
                        if (!Eq(xa[i], xb[i]))
                            return false;
                    }
                    return true;
                }
                case JsonValueKind.Object:
                {
                    var oa = a!.AsObject();
                    var ob = b!.AsObject();
                    if (oa.Count != ob.Count)
                        return false;
                    foreach (var (key, value) in oa)
                    {
                        if (!ob.TryGetPropertyValue(key, out var other) || !Eq(value, other))
                            return false;
                    }
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Python <c>a == "literal"</c>.</summary>
        public static bool Eq(JsonNode? a, string b)
            => AsStringOrNull(a) is { } s && string.Equals(s, b, StringComparison.Ordinal);

        /// <summary>
        /// The key for a lookup in a str-keyed dict (Python <c>x in d</c> / <c>d.get(x)</c>):
        /// the string for a str, null for other hashables (they never match a str key),
        /// a crash for unhashable lists/dicts.
        /// </summary>
        public static string? HashKey(JsonNode? n)
        {
            var k = Kind(n);
            if (k is JsonValueKind.Array or JsonValueKind.Object)
                throw Crash($"TypeError: unhashable type: '{TypeName(n)}'");
            return k == JsonValueKind.String ? AsStringOrNull(n) : null;
        }

        /// <summary>
        /// A canonical key for a dict keyed by arbitrary hashables (the idempotency cache):
        /// equal Python values (1 == 1.0 == True) map to the same key.
        /// </summary>
        public static string AnyHashKey(JsonNode? n)
        {
            var k = Kind(n);
            if (k is JsonValueKind.Array or JsonValueKind.Object)
                throw Crash($"TypeError: unhashable type: '{TypeName(n)}'");
            if (k == JsonValueKind.String)
                return "s:" + AsStringOrNull(n);
            if (IsNumeric(k))
            {
                var num = NumericOf(n!);
                if (!num.IsInt && num.Float == Math.Floor(num.Float) && Math.Abs(num.Float) < 9.2e18)
                    return "n:" + ((long)num.Float).ToString(CultureInfo.InvariantCulture);
                return "n:" + (num.IsInt
                    ? num.Int.ToString(CultureInfo.InvariantCulture)
                    : num.Float.ToString("R", CultureInfo.InvariantCulture));
            }
            return "none";
        }

        /// <summary>Python <c>str(x)</c>.</summary>
        public static string Str(JsonNode? n)
            => Kind(n) == JsonValueKind.String ? AsStringOrNull(n)! : Repr(n);

        /// <summary>Python <c>repr(x)</c> (used by <c>str()</c> of non-strings and of containers).</summary>
        public static string Repr(JsonNode? n)
        {
            switch (Kind(n))
            {
                case JsonValueKind.String:
                    return ReprString(AsStringOrNull(n)!);
                case JsonValueKind.True:
                    return "True";
                case JsonValueKind.False:
                    return "False";
                case JsonValueKind.Number:
                    var num = Number(n!);
                    return num.IsInt ? num.Int.ToString(CultureInfo.InvariantCulture) : FloatRepr(num.Float);
                case JsonValueKind.Array:
                    return "[" + string.Join(", ", n!.AsArray().Select(Repr)) + "]";
                case JsonValueKind.Object:
                    return "{" + string.Join(", ", n!.AsObject().Select(kv => ReprString(kv.Key) + ": " + Repr(kv.Value))) + "}";
                default:
                    return "None";
            }
        }

        private static string ReprString(string s)
        {
            var quote = s.Contains('\'') && !s.Contains('"') ? '"' : '\'';
            var sb = new StringBuilder(s.Length + 2);
            sb.Append(quote);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c == quote)
                            sb.Append('\\');
                        sb.Append(c);
                        break;
                }
            }
            sb.Append(quote);
            return sb.ToString();
        }

        private static string FloatRepr(double d)
        {
            if (double.IsNaN(d))
                return "nan";
            if (double.IsPositiveInfinity(d))
                return "inf";
            if (double.IsNegativeInfinity(d))
                return "-inf";
            var r = d.ToString("R", CultureInfo.InvariantCulture);
            var e = r.IndexOf('E', StringComparison.Ordinal);
            if (e >= 0)
            {
                var exp = int.Parse(r[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                return r[..e] + "e" + (exp < 0 ? "-" : "+")
                    + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
            }
            return r.Contains('.') ? r : r + ".0";
        }

        /// <summary>
        /// Python <c>for x in n</c>: list items, the characters of a str, the keys of a dict;
        /// anything else is not iterable (crash). Returns a snapshot.
        /// </summary>
        public static List<JsonNode?> Iter(JsonNode? n)
        {
            switch (Kind(n))
            {
                case JsonValueKind.Array:
                    return n!.AsArray().ToList();
                case JsonValueKind.String:
                    return AsStringOrNull(n)!.EnumerateRunes()
                        .Select(r => (JsonNode?)JsonValue.Create(r.ToString()))
                        .ToList();
                case JsonValueKind.Object:
                    return n!.AsObject().Select(kv => (JsonNode?)JsonValue.Create(kv.Key)).ToList();
                default:
                    throw Crash($"TypeError: '{TypeName(n)}' object is not iterable");
            }
        }

        /// <summary>Python <c>x in lst</c> (by <c>==</c>).</summary>
        public static bool ListContains(JsonArray list, JsonNode? item) => IndexOf(list, item) >= 0;

        /// <summary>Index of the first element <c>== item</c>, or -1.</summary>
        public static int IndexOf(JsonArray list, JsonNode? item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (Eq(list[i], item))
                    return i;
            }
            return -1;
        }

        /// <summary>An int usable as a slice index / in int arithmetic (bool counts as int).</summary>
        public static long Index(JsonNode? n)
        {
            switch (Kind(n))
            {
                case JsonValueKind.True:
                    return 1;
                case JsonValueKind.False:
                    return 0;
                case JsonValueKind.Number:
                    var num = Number(n!);
                    if (num.IsInt)
                        return num.Int;
                    break;
            }
            throw Crash($"TypeError: slice indices must be integers, not '{TypeName(n)}'");
        }

        /// <summary>A number for <c>&lt;</c>/<c>&gt;</c> comparisons with an int (bool counts as int).</summary>
        public static double Real(JsonNode? n)
        {
            var k = Kind(n);
            if (IsNumeric(k))
                return NumericOf(n!).AsDouble;
            throw Crash($"TypeError: '<' not supported between instances of '{TypeName(n)}' and 'int'");
        }

        /// <summary>Python <c>f"{n:03d}"</c> (sign counts towards the width; non-int crashes).</summary>
        public static string FormatD3(JsonNode? n)
        {
            long value;
            switch (Kind(n))
            {
                case JsonValueKind.True:
                    value = 1;
                    break;
                case JsonValueKind.False:
                    value = 0;
                    break;
                case JsonValueKind.Number when Number(n!).IsInt:
                    value = Number(n!).Int;
                    break;
                default:
                    throw Crash($"ValueError: Unknown format code 'd' for object of type '{TypeName(n)}'");
            }
            var sign = value < 0 ? "-" : "";
            var digits = BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture);
            return sign + digits.PadLeft(3 - sign.Length, '0');
        }

        /// <summary>
        /// Python <c>int(s)</c> for a str: optional sign, decimal digits with single
        /// underscores between them, surrounding whitespace allowed. Null when Python would
        /// raise ValueError.
        /// </summary>
        public static BigInteger? ParseInt(string s)
        {
            var t = s.Trim();
            if (t.Length == 0)
                return null;
            var i = 0;
            var negative = false;
            if (t[0] is '+' or '-')
            {
                negative = t[0] == '-';
                i = 1;
            }
            if (i >= t.Length)
                return null;
            var digits = new StringBuilder();
            var previousWasSeparator = true; // no leading underscore
            for (; i < t.Length; i++)
            {
                var c = t[i];
                if (c == '_')
                {
                    if (previousWasSeparator)
                        return null;
                    previousWasSeparator = true;
                    continue;
                }
                if (!char.IsDigit(c))
                    return null;
                digits.Append((char)('0' + (int)char.GetNumericValue(c)));
                previousWasSeparator = false;
            }
            if (previousWasSeparator)
                return null; // trailing underscore
            var value = BigInteger.Parse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
            return negative ? -value : value;
        }

        /// <summary>Python <c>len(s)</c> (code points, not UTF-16 units).</summary>
        public static int Len(string s) => s.EnumerateRunes().Count();

        /// <summary>Python <c>s[:count]</c> (code points).</summary>
        public static string Prefix(string s, int count)
        {
            var sb = new StringBuilder();
            var taken = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                if (taken++ >= count)
                    break;
                sb.Append(rune.ToString());
            }
            return sb.ToString();
        }

        /// <summary>Python <c>bytes.fromhex(s)</c> succeeds (whitespace only between byte pairs).</summary>
        public static bool IsValidFromHex(string s)
        {
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c is ' ' or '\t' or '\n' or '\r' or '\v' or '\f')
                {
                    i++;
                    continue;
                }
                if (i + 1 >= s.Length || !char.IsAsciiHexDigit(c) || !char.IsAsciiHexDigit(s[i + 1]))
                    return false;
                i += 2;
            }
            return true;
        }
    }
}
