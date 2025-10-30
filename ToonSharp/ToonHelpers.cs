using System.Runtime.CompilerServices;
using System.Text;

namespace ToonSharp;

/// <summary>
/// Internal helper methods for TOON serialization and deserialization.
/// </summary>
internal static class ToonHelpers
{
    /// <summary>
    /// Gets the character representation of a delimiter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char GetDelimiterChar(ToonDelimiter delimiter) => delimiter switch
    {
        ToonDelimiter.Comma => ',',
        ToonDelimiter.Tab => '\t',
        ToonDelimiter.Pipe => '|',
        _ => ','
    };

    /// <summary>
    /// Gets the string representation of a delimiter for headers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetDelimiterString(ToonDelimiter delimiter) => delimiter switch
    {
        ToonDelimiter.Comma => "",
        ToonDelimiter.Tab => "\t",
        ToonDelimiter.Pipe => "|",
        _ => ""
    };

    /// <summary>
    /// Tries to parse a delimiter from a character.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDelimiter(char c, out ToonDelimiter delimiter)
    {
        delimiter = c switch
        {
            ',' => ToonDelimiter.Comma,
            '\t' => ToonDelimiter.Tab,
            '|' => ToonDelimiter.Pipe,
            _ => ToonDelimiter.Comma
        };
        return c is ',' or '\t' or '|';
    }

    /// <summary>
    /// Determines if a string needs to be quoted according to TOON quoting rules.
    /// </summary>
    public static bool RequiresQuoting(ReadOnlySpan<char> value, ToonDelimiter delimiter)
    {
        if (value.IsEmpty)
            return true;

        // Check for leading/trailing whitespace
        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
            return true;

        // Check for reserved literals
        if (value is "true" || value is "false" || value is "null")
            return true;

        // Check for hyphen at the start
        if (value[0] == '-')
            return true;

        // Check for numeric patterns
        if (IsNumericLike(value))
            return true;

        var delimiterChar = GetDelimiterChar(delimiter);

        // Check each character
        foreach (var c in value)
        {
            if (c == ':' || c == '"' || c == '\\' ||
                c == '[' || c == ']' || c == '{' || c == '}' ||
                c == '\n' || c == '\r' || c == '\t' ||
                c == delimiterChar)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a string looks like a number.
    /// </summary>
    private static bool IsNumericLike(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return false;

        var span = value;
        var index = 0;

        // Optional negative sign
        if (span[0] == '-')
        {
            if (span.Length == 1)
                return false;
            index = 1;
        }

        // Check for leading zeros (e.g., "05", "0001")
        if (span.Length > index + 1 && span[index] == '0' && char.IsDigit(span[index + 1]))
            return true; // Forbidden leading zeros

        var hasDigit = false;
        var hasDot = false;
        var hasE = false;

        for (; index < span.Length; index++)
        {
            var c = span[index];

            if (char.IsDigit(c))
            {
                hasDigit = true;
            }
            else if (c == '.' && !hasDot && !hasE)
            {
                hasDot = true;
            }
            else if ((c == 'e' || c == 'E') && !hasE && hasDigit)
            {
                hasE = true;
                hasDigit = false; // Reset for exponent part

                // Check for optional sign after 'e'
                if (index + 1 < span.Length && (span[index + 1] == '+' || span[index + 1] == '-'))
                    index++;
            }
            else
            {
                return false;
            }
        }

        return hasDigit;
    }

    /// <summary>
    /// Determines if a key can be unquoted (matches an identifier pattern).
    /// </summary>
    public static bool IsValidUnquotedKey(ReadOnlySpan<char> key)
    {
        if (key.IsEmpty)
            return false;

        var first = key[0];
        if (!char.IsLetter(first) && first != '_')
            return false;

        for (int i = 1; i < key.Length; i++)
        {
            var c = key[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Escapes a string for use in TOON format.
    /// </summary>
    public static string Escape(ReadOnlySpan<char> value)
    {
        // Fast path: no escaping needed
        var needsEscaping = false;
        foreach (var c in value)
        {
            if (c is '\\' or '"' or '\n' or '\r' or '\t')
            {
                needsEscaping = true;
                break;
            }
        }

        if (!needsEscaping)
            return new string(value);

        var sb = new StringBuilder(value.Length + 4);

        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Unescapes a TOON-escaped string.
    /// </summary>
    public static string Unescape(ReadOnlySpan<char> value)
    {
        var index = value.IndexOf('\\');
        if (index < 0)
            return new string(value);

        var sb = new StringBuilder(value.Length);
        var pos = 0;

        while (index >= 0)
        {
            // Append characters before the escape
            sb.Append(value.Slice(pos, index - pos));

            if (index + 1 >= value.Length)
                throw new ToonException("Unterminated escape sequence");

            var escapeChar = value[index + 1];
            switch (escapeChar)
            {
                case '\\':
                    sb.Append('\\');
                    break;
                case '"':
                    sb.Append('"');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                default:
                    throw new ToonException($"Invalid escape sequence: \\{escapeChar}");
            }

            pos = index + 2;
            index = value.Slice(pos).IndexOf('\\');
            if (index >= 0)
                index += pos;
        }

        // Append remaining characters
        if (pos < value.Length)
            sb.Append(value.Slice(pos));

        return sb.ToString();
    }

    /// <summary>
    /// Creates an indentation string with the specified depth and size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetIndentation(int depth, int indentSize)
    {
        if (depth == 0)
            return string.Empty;

        var totalSpaces = depth * indentSize;
        return new string(' ', totalSpaces);
    }
}
