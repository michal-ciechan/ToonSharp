using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ToonSharp;

/// <summary>
/// Writes TOON-formatted output from JsonNode structures.
/// </summary>
internal sealed class ToonWriter
{
    private readonly ToonSerializerOptions _options;
    private readonly StringBuilder _sb;

    public ToonWriter(ToonSerializerOptions options)
    {
        _options = options;
        _sb = new StringBuilder();
    }

    public string Write(JsonNode? node)
    {
        _sb.Clear();

        if (node is null)
        {
            _sb.Append("null");
            return _sb.ToString();
        }

        WriteValue(node, 0, _options.Delimiter, isRoot: true);

        // Spec: No trailing newline at end of document
        return _sb.ToString();
    }

    private void WriteValue(JsonNode node, int depth, ToonDelimiter activeDelimiter, bool isRoot = false, string? forcedKey = null)
    {
        switch (node)
        {
            case JsonObject obj:
                if (isRoot)
                {
                    WriteObject(obj, depth, activeDelimiter);
                }
                else if (forcedKey is not null)
                {
                    WriteKey(forcedKey, depth);
                    if (obj.Count == 0)
                    {
                        // Empty object
                        _sb.Append(':');
                    }
                    else
                    {
                        _sb.AppendLine(":");
                        WriteObject(obj, depth + 1, activeDelimiter);
                    }
                }
                else
                {
                    WriteObject(obj, depth, activeDelimiter);
                }
                break;

            case JsonArray arr:
                if (isRoot)
                {
                    WriteRootArray(arr, depth, activeDelimiter);
                }
                else if (forcedKey is not null)
                {
                    WriteArrayWithKey(forcedKey, arr, depth, activeDelimiter);
                }
                else
                {
                    throw new ToonException("Array without key in non-root context");
                }
                break;

            case JsonValue val:
                if (forcedKey is not null)
                {
                    WriteKey(forcedKey, depth);
                    _sb.Append(": ");
                    WritePrimitive(val, activeDelimiter);
                }
                else if (isRoot)
                {
                    WritePrimitive(val, activeDelimiter);
                }
                else
                {
                    WritePrimitive(val, activeDelimiter);
                }
                break;
        }
    }

    private void WriteObject(JsonObject obj, int depth, ToonDelimiter activeDelimiter)
    {
        if (obj.Count == 0 && depth == 0)
        {
            // Empty root object = empty document
            return;
        }

        var first = true;
        foreach (var kvp in obj)
        {
            if (!first)
                _sb.AppendLine();
            first = false;

            var key = kvp.Key;
            var value = kvp.Value;

            if (value is null)
            {
                WriteKey(key, depth);
                _sb.Append(": null");
            }
            else
            {
                WriteValue(value, depth, activeDelimiter, isRoot: false, forcedKey: key);
            }
        }
    }

    private void WriteRootArray(JsonArray array, int depth, ToonDelimiter activeDelimiter)
    {
        WriteArrayInternal(null, array, depth, activeDelimiter);
    }

    private void WriteArrayWithKey(string key, JsonArray array, int depth, ToonDelimiter activeDelimiter)
    {
        WriteArrayInternal(key, array, depth, activeDelimiter);
    }

    private void WriteArrayInternal(string? key, JsonArray array, int depth, ToonDelimiter activeDelimiter)
    {
        // Determine array format: inline primitives, tabular, or expanded list
        if (array.Count == 0)
        {
            WriteEmptyArray(key, depth, activeDelimiter);
            return;
        }

        if (IsPrimitiveArray(array))
        {
            WriteInlineArray(key, array, depth, activeDelimiter);
        }
        else if (IsTabularArray(array, out var fields))
        {
            if (fields != null) WriteTabularArray(key, array, fields, depth, activeDelimiter);
        }
        else
        {
            WriteExpandedArray(key, array, depth, activeDelimiter);
        }
    }

    private void WriteEmptyArray(string? key, int depth, ToonDelimiter activeDelimiter)
    {
        if (key is not null)
            WriteKey(key, depth);

        _sb.Append('[');
        if (_options.UseLengthMarker)
            _sb.Append('#');
        _sb.Append('0');
        _sb.Append(ToonHelpers.GetDelimiterString(activeDelimiter));
        _sb.Append("]:");
    }

    private bool IsPrimitiveArray(JsonArray array)
    {
        foreach (var item in array)
        {
            if (item is not JsonValue)
                return false;
        }
        return true;
    }

    private bool IsTabularArray(JsonArray array, out List<string>? fields)
    {
        fields = null;

        // All elements must be objects
        if (array.Count == 0)
            return false;

        var firstKeys = new List<string>();
        JsonObject? firstObj = null;

        foreach (var item in array)
        {
            if (item is not JsonObject obj)
                return false;

            if (firstObj is null)
            {
                firstObj = obj;
                firstKeys.AddRange(obj.Select(kvp => kvp.Key));
            }
            else
            {
                // All objects must have the same keys
                if (obj.Count != firstKeys.Count)
                    return false;

                foreach (var key in firstKeys)
                {
                    if (!obj.ContainsKey(key))
                        return false;
                }
            }

            // All values must be primitives
            foreach (var kvp in obj)
            {
                if (kvp.Value is not JsonValue)
                    return false;
            }
        }

        fields = firstKeys;
        return firstKeys.Count > 0;
    }

    private void WriteInlineArray(string? key, JsonArray array, int depth, ToonDelimiter activeDelimiter)
    {
        if (key is not null)
            WriteKey(key, depth);

        WriteArrayHeader(array.Count, activeDelimiter, fields: null);
        _sb.Append(": ");

        var delimiterChar = ToonHelpers.GetDelimiterChar(activeDelimiter);

        for (int i = 0; i < array.Count; i++)
        {
            if (i > 0)
                _sb.Append(delimiterChar);

            var item = array[i];
            if (item is JsonValue val)
            {
                WritePrimitive(val, activeDelimiter);
            }
        }
    }

    private void WriteTabularArray(string? key, JsonArray array, List<string> fields, int depth, ToonDelimiter activeDelimiter)
    {
        if (key is not null)
            WriteKey(key, depth);

        WriteArrayHeader(array.Count, activeDelimiter, fields);
        _sb.AppendLine(":");

        var delimiterChar = ToonHelpers.GetDelimiterChar(activeDelimiter);

        foreach (var item in array)
        {
            if (item is not JsonObject obj)
                continue;

            WriteIndent(depth + 1);

            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    _sb.Append(delimiterChar);

                var fieldValue = obj[fields[i]];
                if (fieldValue is JsonValue val)
                {
                    WritePrimitive(val, activeDelimiter);
                }
                else
                {
                    _sb.Append("null");
                }
            }

            if (item != array[^1])
                _sb.AppendLine();
        }
    }

    private void WriteExpandedArray(string? key, JsonArray array, int depth, ToonDelimiter activeDelimiter)
    {
        if (key is not null)
            WriteKey(key, depth);

        WriteArrayHeader(array.Count, activeDelimiter, fields: null);
        _sb.AppendLine(":");

        for (int i = 0; i < array.Count; i++)
        {
            var item = array[i];
            WriteIndent(depth + 1);
            _sb.Append("- ");

            if (item is JsonValue val)
            {
                WritePrimitive(val, activeDelimiter);
            }
            else if (item is JsonArray innerArray)
            {
                WriteInlineArray(null, innerArray, 0, activeDelimiter);
            }
            else if (item is JsonObject obj)
            {
                WriteObjectAsListItem(obj, depth + 1, activeDelimiter);
            }

            if (i < array.Count - 1)
                _sb.AppendLine();
        }
    }

    private void WriteObjectAsListItem(JsonObject obj, int depth, ToonDelimiter activeDelimiter)
    {
        if (obj.Count == 0)
        {
            // Empty object is just "-"
            return;
        }

        // First field on the hyphen line (no indentation, already on "- " line)
        var first = true;
        foreach (var kvp in obj)
        {
            if (first)
            {
                first = false;
                var key = kvp.Key;
                var value = kvp.Value;

                // Write key without indentation
                WriteKeyUnquoted(key);

                if (value is null)
                {
                    _sb.Append(": null");
                }
                else if (value is JsonValue val)
                {
                    _sb.Append(": ");
                    WritePrimitive(val, activeDelimiter);
                }
                else if (value is JsonArray arr)
                {
                    WriteArrayHeader(arr.Count, activeDelimiter, fields: null);
                    if (IsPrimitiveArray(arr))
                    {
                        _sb.Append(": ");
                        WriteInlineArrayValues(arr, activeDelimiter);
                    }
                    else if (IsTabularArray(arr, out var fields))
                    {
                        WriteArrayHeader(arr.Count, activeDelimiter, fields);
                        _sb.AppendLine(":");
                        if (fields != null) WriteTabularArrayRows(arr, fields, depth, activeDelimiter);
                    }
                    else
                    {
                        _sb.AppendLine(":");
                        WriteExpandedArrayValues(arr, depth, activeDelimiter);
                    }
                }
                else if (value is JsonObject nestedObj)
                {
                    _sb.AppendLine(":");
                    WriteObject(nestedObj, depth + 2, activeDelimiter); // +2 for nested object in list item
                }
            }
            else
            {
                _sb.AppendLine();
                var key = kvp.Key;
                var value = kvp.Value;

                if (value is null)
                {
                    WriteKey(key, depth);
                    _sb.Append(": null");
                }
                else
                {
                    WriteValue(value, depth, activeDelimiter, isRoot: false, forcedKey: key);
                }
            }
        }
    }

    private void WriteInlineArrayValues(JsonArray array, ToonDelimiter activeDelimiter)
    {
        var delimiterChar = ToonHelpers.GetDelimiterChar(activeDelimiter);
        for (int i = 0; i < array.Count; i++)
        {
            if (i > 0)
                _sb.Append(delimiterChar);

            if (array[i] is JsonValue val)
            {
                WritePrimitive(val, activeDelimiter);
            }
        }
    }

    private void WriteTabularArrayRows(JsonArray array, List<string> fields, int depth, ToonDelimiter activeDelimiter)
    {
        var delimiterChar = ToonHelpers.GetDelimiterChar(activeDelimiter);

        foreach (var item in array)
        {
            if (item is not JsonObject obj)
                continue;

            WriteIndent(depth + 1);

            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    _sb.Append(delimiterChar);

                var fieldValue = obj[fields[i]];
                if (fieldValue is JsonValue val)
                {
                    WritePrimitive(val, activeDelimiter);
                }
                else
                {
                    _sb.Append("null");
                }
            }

            if (item != array[^1])
                _sb.AppendLine();
        }
    }

    private void WriteExpandedArrayValues(JsonArray array, int depth, ToonDelimiter activeDelimiter)
    {
        for (int i = 0; i < array.Count; i++)
        {
            var item = array[i];
            WriteIndent(depth + 1);
            _sb.Append("- ");

            if (item is JsonValue val)
            {
                WritePrimitive(val, activeDelimiter);
            }
            else if (item is JsonArray innerArray)
            {
                WriteInlineArray(null, innerArray, 0, activeDelimiter);
            }
            else if (item is JsonObject obj)
            {
                WriteObjectAsListItem(obj, depth + 1, activeDelimiter);
            }

            if (i < array.Count - 1)
                _sb.AppendLine();
        }
    }

    private void WriteArrayHeader(int count, ToonDelimiter delimiter, List<string>? fields)
    {
        _sb.Append('[');

        if (_options.UseLengthMarker)
            _sb.Append('#');

        _sb.Append(count);
        _sb.Append(ToonHelpers.GetDelimiterString(delimiter));
        _sb.Append(']');

        if (fields is not null && fields.Count > 0)
        {
            _sb.Append('{');
            var delimiterChar = ToonHelpers.GetDelimiterChar(delimiter);

            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    _sb.Append(delimiterChar);

                WriteKeyUnquoted(fields[i]);
            }

            _sb.Append('}');
        }
    }

    private void WritePrimitive(JsonValue value, ToonDelimiter activeDelimiter)
    {
        var obj = value.GetValue<object>();

        switch (obj)
        {
            case null:
                _sb.Append("null");
                break;

            case bool b:
                _sb.Append(b ? "true" : "false");
                break;

            case string s:
                WriteString(s, activeDelimiter);
                break;

            case JsonElement elem:
                WritePrimitiveFromElement(elem, activeDelimiter);
                break;

            default:
                // Numbers
                if (obj is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                {
                    WriteNumber(obj);
                }
                else
                {
                    WriteString(obj.ToString() ?? "null", activeDelimiter);
                }
                break;
        }
    }

    private void WritePrimitiveFromElement(JsonElement elem, ToonDelimiter activeDelimiter)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.String:
                WriteString(elem.GetString() ?? "", activeDelimiter);
                break;
            case JsonValueKind.Number:
                WriteNumber(elem.GetDouble());
                break;
            case JsonValueKind.True:
                _sb.Append("true");
                break;
            case JsonValueKind.False:
                _sb.Append("false");
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                _sb.Append("null");
                break;
        }
    }

    private void WriteNumber(object num)
    {
        // Spec: Numbers must be rendered without scientific notation, -0 → 0
        var str = num switch
        {
            float f => NormalizeNumber(f),
            double d => NormalizeNumber(d),
            decimal m => m.ToString("0.##################", CultureInfo.InvariantCulture),
            _ => Convert.ToString(num, CultureInfo.InvariantCulture) ?? "0"
        };

        _sb.Append(str);
    }

    private string NormalizeNumber(double d)
    {
        // Handle -0
        if (d == 0)
            return "0";

        // Handle NaN and Infinity (should be null per spec)
        if (double.IsNaN(d) || double.IsInfinity(d))
            return "null";

        // Format without scientific notation
        return d.ToString("0.##################", CultureInfo.InvariantCulture);
    }

    private void WriteString(string str, ToonDelimiter activeDelimiter)
    {
        if (ToonHelpers.RequiresQuoting(str, activeDelimiter))
        {
            _sb.Append('"');
            _sb.Append(ToonHelpers.Escape(str));
            _sb.Append('"');
        }
        else
        {
            _sb.Append(str);
        }
    }

    private void WriteKey(string key, int depth)
    {
        WriteIndent(depth);

        if (ToonHelpers.IsValidUnquotedKey(key))
        {
            _sb.Append(key);
        }
        else
        {
            _sb.Append('"');
            _sb.Append(ToonHelpers.Escape(key));
            _sb.Append('"');
        }
    }

    private void WriteKeyUnquoted(string key)
    {
        if (ToonHelpers.IsValidUnquotedKey(key))
        {
            _sb.Append(key);
        }
        else
        {
            _sb.Append('"');
            _sb.Append(ToonHelpers.Escape(key));
            _sb.Append('"');
        }
    }

    private void WriteIndent(int depth)
    {
        if (depth > 0)
        {
            _sb.Append(ToonHelpers.GetIndentation(depth, _options.IndentSize));
        }
    }
}
