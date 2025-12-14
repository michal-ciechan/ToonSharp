using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit;

namespace ToonSharp.Tests.SpecTests;

/// <summary>
/// Runs official TOON specification tests from JSON fixture files.
/// </summary>
public class SpecTestRunner
{
    private static readonly string SpecsPath = Path.Combine(
        AppContext.BaseDirectory,
        "SpecTests",
        "Specs");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    #region Test Data Providers

    public static IEnumerable<object[]> GetEncodeTests() => LoadTests("encode");
    public static IEnumerable<object[]> GetDecodeTests() => LoadTests("decode");

    private static IEnumerable<object[]> LoadTests(string category)
    {
        var categoryPath = Path.Combine(SpecsPath, category);
        if (!Directory.Exists(categoryPath))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(categoryPath, "*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var json = File.ReadAllText(file);
            var fixture = JsonSerializer.Deserialize<SpecFixture>(json, JsonOptions);

            if (fixture?.Tests == null) continue;

            foreach (var test in fixture.Tests)
            {
                // Skip tests that require a newer spec version than 1.3
                if (!string.IsNullOrEmpty(test.MinSpecVersion) &&
                    Version.TryParse(test.MinSpecVersion, out var minVersion) &&
                    minVersion > new Version(1, 3))
                {
                    continue;
                }

                yield return new object[] { fileName, test.Name, test };
            }
        }
    }

    #endregion

    #region Encode Tests

    [Theory]
    [MemberData(nameof(GetEncodeTests))]
    public void Encode_SpecTest(string file, string name, SpecTest test)
    {
        // file and name are used for test display in the test explorer
        _ = file;
        _ = name;

        // Arrange
        var options = MapOptions(test.Options);
        var input = test.Input;

        if (test.ShouldError)
        {
            // Act & Assert - expect exception
            Assert.ThrowsAny<Exception>(() =>
            {
                var obj = ConvertToObject(input);
                ToonSerializer.Serialize(obj, options);
            });
        }
        else
        {
            // Act
            var obj = ConvertToObject(input);
            var actual = ToonSerializer.Serialize(obj, options);

            // Normalize line endings (TOON spec requires LF only)
            actual = actual.Replace("\r\n", "\n");

            // Assert - exact string match including whitespace
            var expected = test.Expected?.GetValue<string>() ?? "";
            Assert.Equal(expected, actual);
        }
    }

    #endregion

    #region Decode Tests

    [Theory]
    [MemberData(nameof(GetDecodeTests))]
    public void Decode_SpecTest(string file, string name, SpecTest test)
    {
        // file and name are used for test display in the test explorer
        _ = file;
        _ = name;

        // Arrange
        var options = MapOptions(test.Options);
        var input = test.Input?.GetValue<string>() ?? "";

        if (test.ShouldError)
        {
            // Act & Assert - expect exception
            Assert.ThrowsAny<Exception>(() =>
            {
                ToonSerializer.Deserialize(input, options);
            });
        }
        else
        {
            // Act
            var actual = ToonSerializer.Deserialize(input, options);

            // Assert - deep equals for JSON comparison
            var expected = test.Expected;
            Assert.True(
                JsonNode.DeepEquals(expected, actual),
                $"JSON mismatch.\nExpected: {expected?.ToJsonString()}\nActual: {actual?.ToJsonString()}");
        }
    }

    #endregion

    #region Helper Methods

    private static ToonSerializerOptions MapOptions(SpecTestOptions? testOptions)
    {
        var options = new ToonSerializerOptions();

        if (testOptions == null) return options;

        if (testOptions.Delimiter != null)
        {
            options.Delimiter = testOptions.Delimiter switch
            {
                "," => ToonDelimiter.Comma,
                "\t" => ToonDelimiter.Tab,
                "|" => ToonDelimiter.Pipe,
                _ => ToonDelimiter.Comma
            };
        }

        if (testOptions.Indent.HasValue)
        {
            options.IndentSize = testOptions.Indent.Value;
        }

        if (testOptions.Strict.HasValue)
        {
            options.Strict = testOptions.Strict.Value;
        }

        return options;
    }

    private static object? ConvertToObject(JsonNode? node)
    {
        if (node == null) return null;

        return node switch
        {
            JsonValue value => ConvertJsonValue(value),
            JsonArray array => array,
            JsonObject obj => obj,
            _ => node
        };
    }

    private static object? ConvertJsonValue(JsonValue value)
    {
        // Try to get the underlying value
        if (value.TryGetValue<bool>(out var boolVal)) return boolVal;
        if (value.TryGetValue<long>(out var longVal)) return longVal;
        if (value.TryGetValue<double>(out var doubleVal)) return doubleVal;
        if (value.TryGetValue<string>(out var strVal)) return strVal;

        return value.GetValue<object>();
    }

    #endregion
}

#region Models

public record SpecFixture(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("tests")] List<SpecTest> Tests
);

public record SpecTest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("input")] JsonNode? Input,
    [property: JsonPropertyName("expected")] JsonNode? Expected,
    [property: JsonPropertyName("shouldError")] bool ShouldError = false,
    [property: JsonPropertyName("options")] SpecTestOptions? Options = null,
    [property: JsonPropertyName("specSection")] string? SpecSection = null,
    [property: JsonPropertyName("note")] string? Note = null,
    [property: JsonPropertyName("minSpecVersion")] string? MinSpecVersion = null
);

public record SpecTestOptions(
    [property: JsonPropertyName("delimiter")] string? Delimiter = null,
    [property: JsonPropertyName("indent")] int? Indent = null,
    [property: JsonPropertyName("strict")] bool? Strict = null,
    [property: JsonPropertyName("keyFolding")] string? KeyFolding = null,
    [property: JsonPropertyName("flattenDepth")] int? FlattenDepth = null,
    [property: JsonPropertyName("expandPaths")] string? ExpandPaths = null
);

#endregion
