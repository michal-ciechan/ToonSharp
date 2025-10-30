namespace ToonSharp;

/// <summary>
/// Specifies the delimiter character used for separating array values and tabular fields in TOON format.
/// </summary>
public enum ToonDelimiter : byte
{
    /// <summary>
    /// Comma delimiter (,) - the default delimiter.
    /// </summary>
    Comma = 0,

    /// <summary>
    /// Tab delimiter (HTAB, U+0009) for tab-separated values.
    /// </summary>
    Tab = 1,

    /// <summary>
    /// Pipe delimiter (|) for pipe-separated values.
    /// </summary>
    Pipe = 2
}
