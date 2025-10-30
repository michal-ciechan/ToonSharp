using System.Globalization;
using System.Text.Json.Nodes;

namespace ToonSharp;

/// <summary>
/// Reads TOON-formatted text and produces JsonNode structures.
/// </summary>
internal sealed class ToonReader
{
    private readonly ToonSerializerOptions _options;
    private string[] _lines = [];
    private int _currentLine;

    public ToonReader(ToonSerializerOptions options)
    {
        _options = options;
    }

    public JsonNode? Read(string toon)
    {
        _lines = toon.Split('\n');
        _currentLine = 0;

        // Skip blank lines and handle empty input
        var nonEmptyLines = _lines
            .Select((line, index) => (line, index))
            .Where(x => !IsBlankLine(x.line))
            .ToList();

        if (nonEmptyLines.Count == 0)
        {
            if (_options.Strict)
                throw new ToonException("Empty input");
            return null;
        }

        // Determine root form
        var firstLine = nonEmptyLines[0].line;
        var firstDepth = GetDepth(firstLine, nonEmptyLines[0].index + 1);

        if (firstDepth != 0)
            throw new ToonException("First non-empty line must be at depth 0", nonEmptyLines[0].index + 1);

        // Check if it's a root array header
        if (IsRootArrayHeader(firstLine))
        {
            return ParseRootArray();
        }

        // Check if it's a single primitive (exactly one line, not a header, not key:value)
        if (nonEmptyLines.Count == 1 && !firstLine.Contains(':'))
        {
            return ParsePrimitive(firstLine.Trim(), ToonDelimiter.Comma);
        }

        // Otherwise, it's an object
        return ParseRootObject();
    }

    private bool IsBlankLine(string line)
    {
        return string.IsNullOrWhiteSpace(line);
    }

    private bool IsRootArrayHeader(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('[') && trimmed.Contains(']') && trimmed.Contains(':');
    }

    private JsonNode ParseRootObject()
    {
        var obj = new JsonObject();
        ParseObjectFields(obj, 0);
        return obj;
    }

    private JsonNode ParseRootArray()
    {
        var line = GetCurrentLine();
        var header = ParseArrayHeader(line, _currentLine + 1, null);
        _currentLine++;

        var array = new JsonArray();

        if (header.Count == 0)
            return array;

        if (header.IsInline)
        {
            // Inline primitive array
            ParseInlineArrayValues(array, header);
        }
        else if (header.Fields is not null)
        {
            // Tabular array
            ParseTabularRows(array, header, 1);
        }
        else
        {
            // Expanded array
            ParseExpandedArrayItems(array, header, 1);
        }

        return array;
    }

    private void ParseObjectFields(JsonObject obj, int expectedDepth)
    {
        while (_currentLine < _lines.Length)
        {
            var line = _lines[_currentLine];

            if (IsBlankLine(line))
            {
                _currentLine++;
                continue;
            }

            var depth = GetDepth(line, _currentLine + 1);

            if (depth < expectedDepth)
                break;

            if (depth > expectedDepth)
                throw new ToonException($"Unexpected indentation depth {depth}, expected {expectedDepth}", _currentLine + 1);

            var trimmed = line.TrimStart();

            // Parse key-value or nested structure
            var colonIndex = FindUnquotedChar(trimmed, ':');
            if (colonIndex < 0)
            {
                if (_options.Strict)
                    throw new ToonException("Missing colon after key", _currentLine + 1);
                _currentLine++;
                continue;
            }

            var keyPart = trimmed.Substring(0, colonIndex);
            var valuePart = trimmed.Substring(colonIndex + 1).TrimStart();

            // Check if it's an array header
            if (keyPart.Contains('[') && keyPart.Contains(']'))
            {
                // Extract key before the bracket
                var bracketIndex = keyPart.IndexOf('[');
                var keyBeforeBracket = keyPart.Substring(0, bracketIndex);
                var key = ParseKey(keyBeforeBracket);

                var header = ParseArrayHeader(trimmed, _currentLine + 1, key);
                _currentLine++;

                var array = new JsonArray();

                if (header.IsInline)
                {
                    ParseInlineArrayValues(array, header);
                }
                else if (header.Fields is not null)
                {
                    ParseTabularRows(array, header, expectedDepth + 1);
                }
                else
                {
                    ParseExpandedArrayItems(array, header, expectedDepth + 1);
                }

                obj[key] = array;
            }
            else
            {
                var key = ParseKey(keyPart);

                if (string.IsNullOrWhiteSpace(valuePart))
                {
                    // Nested object
                    _currentLine++;
                    var nestedObj = new JsonObject();
                    ParseObjectFields(nestedObj, expectedDepth + 1);
                    obj[key] = nestedObj;
                }
                else
                {
                    // Primitive value
                    var value = ParsePrimitive(valuePart, _options.Delimiter);
                    obj[key] = value;
                    _currentLine++;
                }
            }
        }
    }

    private void ParseInlineArrayValues(JsonArray array, ArrayHeader header)
    {
        if (string.IsNullOrWhiteSpace(header.InlineValues))
            return;

        var values = SplitByDelimiter(header.InlineValues, header.Delimiter);

        if (_options.Strict && values.Count != header.Count)
        {
            throw new ToonException(
                $"Array count mismatch: expected {header.Count}, got {values.Count}",
                _currentLine + 1);
        }

        foreach (var val in values)
        {
            array.Add(ParsePrimitive(val.Trim(), header.Delimiter));
        }
    }

    private void ParseTabularRows(JsonArray array, ArrayHeader header, int expectedDepth)
    {
        var rowCount = 0;

        while (_currentLine < _lines.Length)
        {
            var line = _lines[_currentLine];

            if (IsBlankLine(line))
            {
                if (_options.Strict)
                    throw new ToonException("Blank lines not allowed inside tabular arrays", _currentLine + 1);
                _currentLine++;
                continue;
            }

            var depth = GetDepth(line, _currentLine + 1);

            if (depth < expectedDepth)
                break;

            if (depth > expectedDepth)
                throw new ToonException($"Unexpected indentation in tabular row", _currentLine + 1);

            var trimmed = line.TrimStart();

            // Check if this is a row or a new key-value line
            if (IsTabularRow(trimmed, header.Delimiter))
            {
                var values = SplitByDelimiter(trimmed, header.Delimiter);

                if (_options.Strict && header.Fields != null && values.Count != header.Fields.Count)
                {
                    throw new ToonException(
                        $"Row width mismatch: expected {header.Fields.Count} values, got {values.Count}",
                        _currentLine + 1);
                }

                var obj = new JsonObject();
                if (header.Fields != null)
                    for (int i = 0; i < Math.Min(values.Count, header.Fields.Count); i++)
                    {
                        obj[header.Fields[i]] = ParsePrimitive(values[i].Trim(), header.Delimiter);
                    }

                array.Add(obj);
                rowCount++;
                _currentLine++;
            }
            else
            {
                // End of rows
                break;
            }
        }

        if (_options.Strict && rowCount != header.Count)
        {
            throw new ToonException(
                $"Tabular array count mismatch: expected {header.Count} rows, got {rowCount}",
                _currentLine + 1);
        }
    }

    private bool IsTabularRow(string line, ToonDelimiter delimiter)
    {
        var delimiterIndex = FindUnquotedChar(line, ToonHelpers.GetDelimiterChar(delimiter));
        var colonIndex = FindUnquotedChar(line, ':');

        if (colonIndex < 0)
            return true; // No colon = row

        if (delimiterIndex < 0)
            return false; // Colon but no delimiter = key-value

        return delimiterIndex < colonIndex; // Delimiter before colon = row
    }

    private void ParseExpandedArrayItems(JsonArray array, ArrayHeader header, int expectedDepth)
    {
        var itemCount = 0;

        while (_currentLine < _lines.Length)
        {
            var line = _lines[_currentLine];

            if (IsBlankLine(line))
            {
                if (_options.Strict)
                    throw new ToonException("Blank lines not allowed inside arrays", _currentLine + 1);
                _currentLine++;
                continue;
            }

            var depth = GetDepth(line, _currentLine + 1);

            if (depth < expectedDepth)
                break;

            if (depth > expectedDepth)
                throw new ToonException($"Unexpected indentation in array item", _currentLine + 1);

            var trimmed = line.TrimStart();

            if (!trimmed.StartsWith("- "))
                break;

            var itemContent = trimmed.Substring(2);

            // Determine item type
            if (itemContent.StartsWith('['))
            {
                // Inline array item
                var itemHeader = ParseArrayHeader(itemContent, _currentLine + 1, null);
                var innerArray = new JsonArray();
                ParseInlineArrayValues(innerArray, itemHeader);
                array.Add(innerArray);
                _currentLine++;
            }
            else if (itemContent.Contains(':'))
            {
                // Object item
                var obj = ParseObjectListItem(itemContent, expectedDepth);
                array.Add(obj);
            }
            else
            {
                // Primitive item
                array.Add(ParsePrimitive(itemContent, header.Delimiter));
                _currentLine++;
            }

            itemCount++;
        }

        if (_options.Strict && itemCount != header.Count)
        {
            throw new ToonException(
                $"Array count mismatch: expected {header.Count} items, got {itemCount}",
                _currentLine + 1);
        }
    }

    private JsonObject ParseObjectListItem(string firstFieldLine, int itemDepth)
    {
        var obj = new JsonObject();

        // Parse first field from the hyphen line
        var colonIndex = FindUnquotedChar(firstFieldLine, ':');
        if (colonIndex < 0)
        {
            if (_options.Strict)
                throw new ToonException("Missing colon in object field", _currentLine + 1);
            _currentLine++;
            return obj;
        }

        var keyPart = firstFieldLine.Substring(0, colonIndex);
        var valuePart = firstFieldLine.Substring(colonIndex + 1).TrimStart();

        var key = ParseKey(keyPart);

        // Check if it's an array
        if (keyPart.Contains('[') && keyPart.Contains(']'))
        {
            var header = ParseArrayHeader(firstFieldLine, _currentLine + 1, key);
            _currentLine++;

            var array = new JsonArray();

            if (header.IsInline)
            {
                ParseInlineArrayValues(array, header);
            }
            else if (header.Fields is not null)
            {
                ParseTabularRows(array, header, itemDepth + 1);
            }
            else
            {
                ParseExpandedArrayItems(array, header, itemDepth + 1);
            }

            obj[key] = array;
        }
        else if (string.IsNullOrWhiteSpace(valuePart))
        {
            // Nested object - fields at depth + 2
            _currentLine++;
            var nestedObj = new JsonObject();
            ParseObjectFields(nestedObj, itemDepth + 2);
            obj[key] = nestedObj;
        }
        else
        {
            obj[key] = ParsePrimitive(valuePart, _options.Delimiter);
            _currentLine++;
        }

        // Parse remaining fields at itemDepth
        ParseObjectFields(obj, itemDepth);

        return obj;
    }

    private ArrayHeader ParseArrayHeader(string line, int lineNumber, string? key)
    {
        var bracketStart = line.IndexOf('[');
        var bracketEnd = line.IndexOf(']');

        if (bracketStart < 0 || bracketEnd < 0 || bracketEnd <= bracketStart)
            throw new ToonException("Invalid array header", lineNumber);

        var bracketContent = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

        // Parse length marker and delimiter
        var hasLengthMarker = bracketContent.StartsWith('#');
        if (hasLengthMarker)
            bracketContent = bracketContent.Substring(1);

        // Detect delimiter
        var delimiter = ToonDelimiter.Comma;
        if (bracketContent.EndsWith('\t'))
        {
            delimiter = ToonDelimiter.Tab;
            bracketContent = bracketContent.Substring(0, bracketContent.Length - 1);
        }
        else if (bracketContent.EndsWith('|'))
        {
            delimiter = ToonDelimiter.Pipe;
            bracketContent = bracketContent.Substring(0, bracketContent.Length - 1);
        }

        if (!int.TryParse(bracketContent, out var count))
            throw new ToonException($"Invalid array length: {bracketContent}", lineNumber);

        // Parse fields if present
        List<string>? fields = null;
        var fieldsStart = line.IndexOf('{', bracketEnd);
        var fieldsEnd = line.IndexOf('}', bracketEnd);

        if (fieldsStart >= 0 && fieldsEnd > fieldsStart)
        {
            var fieldsContent = line.Substring(fieldsStart + 1, fieldsEnd - fieldsStart - 1);
            var fieldNames = SplitByDelimiter(fieldsContent, delimiter);
            fields = fieldNames.Select(f => ParseKey(f.Trim())).ToList();
        }

        // Check for inline values
        var colonIndex = line.IndexOf(':', bracketEnd);
        if (colonIndex < 0)
            throw new ToonException("Missing colon after array header", lineNumber);

        var afterColon = line.Substring(colonIndex + 1).TrimStart();
        var isInline = !string.IsNullOrWhiteSpace(afterColon);

        return new ArrayHeader
        {
            Key = key,
            Count = count,
            Delimiter = delimiter,
            Fields = fields,
            IsInline = isInline,
            InlineValues = isInline ? afterColon : null,
            HasLengthMarker = hasLengthMarker
        };
    }

    private string ParseKey(string keyText)
    {
        var trimmed = keyText.Trim();

        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            var content = trimmed.Substring(1, trimmed.Length - 2);
            return ToonHelpers.Unescape(content);
        }

        return trimmed;
    }

    private JsonValue? ParsePrimitive(string text, ToonDelimiter delimiter)
    {
        if (string.IsNullOrWhiteSpace(text))
            return JsonValue.Create("");

        var trimmed = text.Trim();

        // Quoted string
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            var content = trimmed.Substring(1, trimmed.Length - 2);
            return JsonValue.Create(ToonHelpers.Unescape(content));
        }

        switch (trimmed)
        {
            // Literals
            case "null":
                return JsonValue.Create((string?)null);
            case "true":
                return JsonValue.Create(true);
            case "false":
                return JsonValue.Create(false);
        }

        // Try parsing as number
        return TryParseNumber(trimmed, out var number) ? JsonValue.Create(number) :
            // Otherwise it's an unquoted string
            JsonValue.Create(trimmed);
    }

    private bool TryParseNumber(string text, out double number)
    {
        // Check for forbidden leading zeros
        if (text.Length <= 1 || text[0] != '0' || !char.IsDigit(text[1]))
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        number = 0;
        return false; // Treat as string

    }

    private List<string> SplitByDelimiter(string text, ToonDelimiter delimiter)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var delimiterChar = ToonHelpers.GetDelimiterChar(delimiter);

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == '\\' && inQuotes && i + 1 < text.Length)
            {
                current.Append(c);
                current.Append(text[++i]);
            }
            else if (c == delimiterChar && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private int FindUnquotedChar(string text, char target)
    {
        var inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '\\' && inQuotes && i + 1 < text.Length)
            {
                i++; // Skip escaped character
            }
            else if (c == target && !inQuotes)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetDepth(string line, int lineNumber)
    {
        var spaces = 0;

        foreach (var c in line)
        {
            if (c == ' ')
            {
                spaces++;
            }
            else if (c == '\t')
            {
                if (_options.Strict)
                    throw new ToonException("Tabs are not allowed in indentation", lineNumber);
                spaces += _options.IndentSize;
            }
            else
            {
                break;
            }
        }

        if (_options.Strict && spaces % _options.IndentSize != 0)
        {
            throw new ToonException(
                $"Indentation must be an exact multiple of {_options.IndentSize} spaces",
                lineNumber);
        }

        return spaces / _options.IndentSize;
    }

    private string GetCurrentLine()
    {
        return _currentLine < _lines.Length ? _lines[_currentLine] : string.Empty;
    }

    private record ArrayHeader
    {
        public required string? Key { get; init; }
        public required int Count { get; init; }
        public required ToonDelimiter Delimiter { get; init; }
        public required List<string>? Fields { get; init; }
        public required bool IsInline { get; init; }
        public required string? InlineValues { get; init; }
        public required bool HasLengthMarker { get; init; }
    }
}
