namespace ToonSharp;

/// <summary>
/// The exception that is thrown when an error occurs during TOON serialization or deserialization.
/// </summary>
public sealed class ToonException : Exception
{
    /// <summary>
    /// Gets the line number where the error occurred, or null if not applicable.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Gets the column number where the error occurred, or null if not applicable.
    /// </summary>
    public int? ColumnNumber { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class.
    /// </summary>
    public ToonException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ToonException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class with a specified error message
    /// and line number.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    public ToonException(string message, int lineNumber) : base(FormatMessage(message, lineNumber, null))
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class with a specified error message,
    /// line number, and column number.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    /// <param name="columnNumber">The column number where the error occurred.</param>
    public ToonException(string message, int lineNumber, int columnNumber)
        : base(FormatMessage(message, lineNumber, columnNumber))
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ToonException(string message, Exception innerException) : base(message, innerException)
    {
    }

    private static string FormatMessage(string message, int lineNumber, int? columnNumber)
    {
        return columnNumber.HasValue
            ? $"Line {lineNumber}, Column {columnNumber}: {message}"
            : $"Line {lineNumber}: {message}";
    }
}
