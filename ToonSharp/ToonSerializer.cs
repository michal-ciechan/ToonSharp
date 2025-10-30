using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ToonSharp;

/// <summary>
/// Provides functionality to serialize objects to TOON format and deserialize TOON data to objects.
/// </summary>
public static class ToonSerializer
{
    /// <summary>
    /// Converts the provided value to a TOON string.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    /// <returns>A TOON string representation of the value.</returns>
    public static string Serialize<TValue>(TValue value, ToonSerializerOptions? options = null)
    {
        options ??= ToonSerializerOptions.Default;

        // Convert to JsonNode first for normalization
        var jsonNode = JsonSerializer.SerializeToNode(value);

        var writer = new ToonWriter(options);
        return writer.Write(jsonNode);
    }

    /// <summary>
    /// Converts the provided value to a TOON string asynchronously.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="stream">The UTF-8 stream to write the TOON data to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SerializeAsync<TValue>(
        Stream stream,
        TValue value,
        ToonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var toonString = Serialize(value, options);
        var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync(toonString.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses the TOON string and returns the result as a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="toon">The TOON string to parse.</param>
    /// <param name="options">Options to control deserialization behavior.</param>
    /// <returns>A <see cref="JsonNode"/> representation of the TOON data.</returns>
    public static JsonNode? Deserialize(string toon, ToonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(toon);

        options ??= ToonSerializerOptions.Default;
        var reader = new ToonReader(options);
        return reader.Read(toon);
    }

    /// <summary>
    /// Parses the TOON string and returns a value of the type specified by a generic type parameter.
    /// </summary>
    /// <typeparam name="TValue">The target type to deserialize to.</typeparam>
    /// <param name="toon">The TOON string to parse.</param>
    /// <param name="options">Options to control deserialization behavior.</param>
    /// <returns>A <typeparamref name="TValue"/> representation of the TOON data.</returns>
    public static TValue? Deserialize<TValue>(string toon, ToonSerializerOptions? options = null)
    {
        var jsonNode = Deserialize(toon, options);
        return jsonNode is null ? default : jsonNode.Deserialize<TValue>();
    }

    /// <summary>
    /// Reads the UTF-8 encoded stream and returns a value of the type specified by a generic type parameter.
    /// </summary>
    /// <typeparam name="TValue">The target type to deserialize to.</typeparam>
    /// <param name="stream">The UTF-8 stream to read the TOON data from.</param>
    /// <param name="options">Options to control deserialization behavior.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation with a <typeparamref name="TValue"/> representation of the TOON data.</returns>
    public static async Task<TValue?> DeserializeAsync<TValue>(
        Stream stream,
        ToonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        var toonString = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Deserialize<TValue>(toonString, options);
    }

    /// <summary>
    /// Attempts to parse the TOON string and returns a value that indicates whether the operation succeeded.
    /// </summary>
    /// <typeparam name="TValue">The target type to deserialize to.</typeparam>
    /// <param name="toon">The TOON string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed value.</param>
    /// <param name="options">Options to control deserialization behavior.</param>
    /// <returns><see langword="true"/> if the TOON string was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryDeserialize<TValue>(
        string toon,
        [NotNullWhen(true)] out TValue? result,
        ToonSerializerOptions? options = null)
    {
        try
        {
            result = Deserialize<TValue>(toon, options);
            return result is not null;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}
