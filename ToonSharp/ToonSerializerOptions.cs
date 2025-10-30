namespace ToonSharp;

/// <summary>
/// Provides options for controlling TOON serialization and deserialization behavior.
/// </summary>
public sealed class ToonSerializerOptions
{
    private int _indentSize = 2;

    /// <summary>
    /// Gets or sets the number of spaces per indentation level.
    /// Default is 2 spaces per level.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1 or greater than 8.
    /// </exception>
    public int IndentSize
    {
        get => _indentSize;
        set
        {
            if (value < 1 || value > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "IndentSize must be between 1 and 8.");
            }
            _indentSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the delimiter character used for array values and tabular fields.
    /// Default is <see cref="ToonDelimiter.Comma"/>.
    /// </summary>
    /// <remarks>
    /// This is the document delimiter used outside of any array scope.
    /// Individual arrays can override this with their own header declarations.
    /// </remarks>
    public ToonDelimiter Delimiter { get; set; } = ToonDelimiter.Comma;

    /// <summary>
    /// Gets or sets whether to include the length marker ("#") in array headers.
    /// Default is false (length marker omitted).
    /// </summary>
    public bool UseLengthMarker { get; set; }

    /// <summary>
    /// Gets or sets whether to enable strict mode during deserialization.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When enabled, the parser enforces:
    /// <list type="bullet">
    /// <item>Array count and tabular row width must match declared lengths</item>
    /// <item>Indentation must be exact multiples of <see cref="IndentSize"/></item>
    /// <item>Tabs cannot be used for indentation</item>
    /// <item>Invalid escape sequences cause errors</item>
    /// <item>Missing colons after keys cause errors</item>
    /// <item>Blank lines inside arrays/tabular rows cause errors</item>
    /// </list>
    /// </remarks>
    public bool Strict { get; set; } = true;

    /// <summary>
    /// Gets the default options with standard settings.
    /// </summary>
    public static ToonSerializerOptions Default { get; } = new();
}
