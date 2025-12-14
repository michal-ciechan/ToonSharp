using System.Text.Json;
using System.Text.Json.Nodes;
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
        "Specs"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Known spec deviations - see README.md "Spec Deviations" section for details.
    /// </summary>
    private static readonly HashSet<string> KnownDeviations =
    [
        // Encode: Hyphen Quoting
        "quotes single hyphen as object value",
        "quotes single hyphen in array",
        "quotes leading-hyphen string in array",

        // Encode: Null in Tabular Format
        "encodes null values in tabular format",

        // Encode: Floating-Point Precision
        "encodes MAX_SAFE_INTEGER",
        "encodes repeating decimal with full precision",

        // Decode: Quoted Keys with Brackets
        "parses quoted key with brackets",
        "parses quoted key containing brackets with inline array",

        // Decode: Quoted Field Names in Tabular
        "parses tabular array with quoted field names",
        "parses quoted header keys in tabular arrays",

        // Decode: Blank Line Handling
        "allows blank line after primitive array",
        "accepts blank line after array ends",

        // Decode: Nested Arrays in List Items
        "parses list-form array with inline arrays",
        "parses nested arrays inside list items with default comma delimiter",
        "parses nested arrays inside list items with default comma delimiter when parent uses pipe",

        // Decode: Delimiter Inheritance in List Items
        "object values in list items follow document delimiter",
        "object values with comma must be quoted when document delimiter is comma",

        // Decode: Negative Leading-Zero Numbers
        "treats unquoted negative leading-zero number as string",
        "treats negative leading-zeros in array as strings",

        // Decode: Root Primitives
        "parses quoted string with backslash escape",
        "parses empty document as empty object",

        // Decode: Unterminated String Detection
        "throws on unterminated string"
    ];

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
                // Skip tests that require a newer spec version than 1.4
                if (!string.IsNullOrEmpty(test.MinSpecVersion) &&
                    Version.TryParse(test.MinSpecVersion, out var minVersion) &&
                    minVersion > new Version(1, 4))
                {
                    continue;
                }

                yield return new object[] { fileName, test.Name, test };
            }
        }
    }

    #endregion

    #region Encode Tests

    [SkippableTheory]
    [MemberData(nameof(GetEncodeTests))]
    public void Encode_SpecTest(string file, string name, SpecTest test)
    {
        // file and name are used for test display in the test explorer
        _ = file;
        _ = name;

        // Skip known deviations - see README.md "Spec Deviations" section
        Skip.If(
            KnownDeviations.Contains(test.Name),
            $"Known deviation: {test.Name} - see README.md Spec Deviations section"
        );

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

    [SkippableTheory]
    [MemberData(nameof(GetDecodeTests))]
    public void Decode_SpecTest(string file, string name, SpecTest test)
    {
        // file and name are used for test display in the test explorer
        _ = file;
        _ = name;

        // Skip known deviations - see README.md "Spec Deviations" section
        Skip.If(
            KnownDeviations.Contains(test.Name),
            $"Known deviation: {test.Name} - see README.md Spec Deviations section"
        );

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
    string Version,
    string Category,
    string Description,
    List<SpecTest> Tests
);

public record SpecTest(
    string Name,
    JsonNode? Input,
    JsonNode? Expected,
    bool ShouldError = false,
    SpecTestOptions? Options = null,
    string? SpecSection = null,
    string? Note = null,
    string? MinSpecVersion = null
);

public record SpecTestOptions(
    string? Delimiter = null,
    int? Indent = null,
    bool? Strict = null,
    string? KeyFolding = null,
    int? FlattenDepth = null,
    string? ExpandPaths = null
);

#endregion
