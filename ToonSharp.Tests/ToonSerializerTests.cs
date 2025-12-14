using System.Text.Json.Nodes;
using Xunit;

namespace ToonSharp.Tests;

public class ToonSerializerTests
{

    [Fact]
    public void Serialize_SimpleObject_ReturnsCorrectToon()
    {
        // Arrange
        var obj = new
        {
            id = 123,
            name = "Ada",
            active = true
        };

        // Act
        var toon = ToonSerializer.Serialize(obj).NormalizeLineEndings();

        // Assert
        var expected = "id: 123\nname: Ada\nactive: true";
        Assert.Equal(expected, toon);
    }

    [Fact]
    public void Serialize_NestedObject_ReturnsCorrectToon()
    {
        // Arrange
        var obj = new
        {
            user = new
            {
                id = 123,
                name = "Ada"
            }
        };

        // Act
        var toon = ToonSerializer.Serialize(obj).NormalizeLineEndings();

        // Assert
        var expected = "user:\n  id: 123\n  name: Ada";
        Assert.Equal(expected, toon);
    }

    [Fact]
    public void Serialize_PrimitiveArray_ReturnsCorrectToon()
    {
        // Arrange
        var obj = new
        {
            tags = new[] { "admin", "ops", "dev" }
        };

        // Act
        var toon = ToonSerializer.Serialize(obj);

        // Assert
        var expected = "tags[3]: admin,ops,dev";
        Assert.Equal(expected, toon);
    }

    [Fact]
    public void Serialize_ArrayOfArrays_ReturnsCorrectToon()
    {
        // Arrange
        var obj = new
        {
            pairs = new[]
            {
                new[] { 1, 2 },
                new[] { 3, 4 }
            }
        };

        // Act
        var toon = ToonSerializer.Serialize(obj).NormalizeLineEndings();

        // Assert
        var expected = "pairs[2]:\n  - [2]: 1,2\n  - [2]: 3,4";
        Assert.Equal(expected, toon);
    }

    [Fact]
    public void Serialize_TabularArray_ReturnsCorrectToon()
    {
        // Arrange
        var obj = new
        {
            items = new[]
            {
                new { sku = "A1", qty = 2, price = 9.99 },
                new { sku = "B2", qty = 1, price = 14.5 }
            }
        };

        // Act
        var toon = ToonSerializer.Serialize(obj).NormalizeLineEndings();

        // Assert
        var expected = "items[2]{sku,qty,price}:\n  A1,2,9.99\n  B2,1,14.5";
        Assert.Equal(expected, toon);
    }

    [Fact]
    public void Serialize_MixedArray_ReturnsCorrectToon()
    {
        // Arrange
        var items = new JsonArray
        {
            JsonValue.Create(1),
            new JsonObject { ["a"] = 1 },
            JsonValue.Create("text")
        };
        var obj = new JsonObject { ["items"] = items };

        // Act
        var toon = ToonSerializer.Serialize(obj).NormalizeLineEndings();

        // Assert
        var expected = "items[3]:\n  - 1\n  - a: 1\n  - text";
        Assert.Equal(expected, toon);
    }

    [Fact]
    public void Serialize_ObjectsAsListItems_ReturnsCorrectToon()
    {
        // Arrange
        var items = new JsonArray
        {
            new JsonObject { ["id"] = 1, ["name"] = "First" },
            new JsonObject { ["id"] = 2, ["name"] = "Second", ["extra"] = true }
        };
        var obj = new JsonObject { ["items"] = items };

        // Act
        var toon = ToonSerializer.Serialize(obj);

        // Assert
        Assert.Contains("items[2]", toon);
    }

    [Fact]
    public void Deserialize_SimpleObject_ReturnsCorrectObject()
    {
        // Arrange
        var toon = "id: 123\nname: Ada\nactive: true";

        // Act
        var result = ToonSerializer.Deserialize(toon);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.Equal(123, obj["id"]!.GetValue<double>());
        Assert.Equal("Ada", obj["name"]!.GetValue<string>());
        Assert.True(obj["active"]!.GetValue<bool>());
    }

    [Fact]
    public void Deserialize_PrimitiveArray_ReturnsCorrectArray()
    {
        // Arrange
        var toon = "tags[3]: admin,ops,dev";

        // Act
        var result = ToonSerializer.Deserialize(toon);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        var tags = obj["tags"] as JsonArray;
        Assert.NotNull(tags);
        Assert.Equal(3, tags.Count);
        Assert.Equal("admin", tags[0]!.GetValue<string>());
        Assert.Equal("ops", tags[1]!.GetValue<string>());
        Assert.Equal("dev", tags[2]!.GetValue<string>());
    }

    [Fact]
    public void Deserialize_TabularArray_ReturnsCorrectObjects()
    {
        // Arrange
        var toon = "items[2]{sku,qty,price}:\n  A1,2,9.99\n  B2,1,14.5";

        // Act
        var result = ToonSerializer.Deserialize(toon);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        var items = obj["items"] as JsonArray;
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var first = items[0] as JsonObject;
        Assert.NotNull(first);
        Assert.Equal("A1", first["sku"]!.GetValue<string>());
        Assert.Equal(2, first["qty"]!.GetValue<double>());
        Assert.Equal(9.99, first["price"]!.GetValue<double>());
    }

    [Fact]
    public void Serialize_EmptyObject_ReturnsEmptyString()
    {
        // Arrange
        var obj = new { };

        // Act
        var toon = ToonSerializer.Serialize(obj);

        // Assert
        Assert.Equal("", toon);
    }

    [Fact]
    public void Serialize_NullValue_ReturnsNull()
    {
        // Arrange
        var obj = new
        {
            value = (string?)null
        };

        // Act
        var toon = ToonSerializer.Serialize(obj);

        // Assert
        Assert.Equal("value: null", toon);
    }

    [Fact]
    public void Serialize_QuotedStrings_HandlesSpecialCharacters()
    {
        // Arrange
        var obj = new
        {
            colon = "a:b",
            comma = "a,b",
            quote = "a\"b",
            newline = "a\nb",
            empty = ""
        };

        // Act
        var toon = ToonSerializer.Serialize(obj);

        // Assert
        Assert.Contains("colon: \"a:b\"", toon);
        Assert.Contains("comma: \"a,b\"", toon);
        Assert.Contains("quote: \"a\\\"b\"", toon);
        Assert.Contains("newline: \"a\\nb\"", toon);
        Assert.Contains("empty: \"\"", toon);
    }

    [Fact]
    public void Serialize_WithTabDelimiter_UsesTabsInHeader()
    {
        // Arrange
        var obj = new
        {
            tags = new[] { "reading", "gaming", "coding" }
        };

        var options = new ToonSerializerOptions
        {
            Delimiter = ToonDelimiter.Tab
        };

        // Act
        var toon = ToonSerializer.Serialize(obj, options);

        // Assert
        Assert.Contains("tags[3\t]:", toon);
        Assert.Contains("\t", toon.Split(':')[1]);
    }

    [Fact]
    public void Serialize_WithPipeDelimiter_UsesPipesInHeader()
    {
        // Arrange
        var obj = new
        {
            tags = new[] { "reading", "gaming", "coding" }
        };

        var options = new ToonSerializerOptions
        {
            Delimiter = ToonDelimiter.Pipe
        };

        // Act
        var toon = ToonSerializer.Serialize(obj, options);

        // Assert
        Assert.Contains("tags[3|]:", toon);
        Assert.Contains("|", toon.Split(':')[1]);
    }

    [Fact]
    public void Serialize_WithLengthMarker_IncludesHashInHeader()
    {
        // Arrange
        var obj = new
        {
            tags = new[] { "reading", "gaming", "coding" }
        };

        var options = new ToonSerializerOptions
        {
            UseLengthMarker = true
        };

        // Act
        var toon = ToonSerializer.Serialize(obj, options);

        // Assert
        Assert.Contains("tags[#3]:", toon);
    }

    [Fact]
    public void RoundTrip_ComplexObject_PreservesData()
    {
        // Arrange
        var original = new
        {
            id = 123,
            name = "Test User",
            scores = new[] { 95, 87, 92 },
            settings = new
            {
                theme = "dark",
                notifications = true
            },
            tags = new[] { "admin", "developer" }
        };

        // Act
        var toon = ToonSerializer.Serialize(original);
        var result = ToonSerializer.Deserialize(toon);

        // Assert
        Assert.NotNull(result);
        var deserialized = result.AsObject();
        Assert.Equal(123, deserialized["id"]!.GetValue<double>());
        Assert.Equal("Test User", deserialized["name"]!.GetValue<string>());

        var scores = deserialized["scores"] as JsonArray;
        Assert.NotNull(scores);
        Assert.Equal(3, scores.Count);

        var settings = deserialized["settings"] as JsonObject;
        Assert.NotNull(settings);
        Assert.Equal("dark", settings["theme"]!.GetValue<string>());
        Assert.True(settings["notifications"]!.GetValue<bool>());
    }

    [Fact]
    public void Deserialize_StrictMode_ThrowsOnCountMismatch()
    {
        // Arrange
        var toon = "tags[3]: admin,ops"; // Only 2 values, not 3

        var options = new ToonSerializerOptions
        {
            Strict = true
        };

        // Act & Assert
        Assert.Throws<ToonException>(() => ToonSerializer.Deserialize(toon, options));
    }

    [Fact]
    public void Deserialize_NonStrictMode_AllowsCountMismatch()
    {
        // Arrange
        var toon = "tags[3]: admin,ops"; // Only 2 values, not 3

        var options = new ToonSerializerOptions
        {
            Strict = false
        };

        // Act
        var result = ToonSerializer.Deserialize(toon, options);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        var tags = obj["tags"] as JsonArray;
        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count); // Should have 2 items despite header saying 3
    }

    [Fact]
    public void Serialize_NumbersWithoutExponent_UsesDecimalNotation()
    {
        // Arrange
        var obj = new
        {
            large = 1000000,
            small = 0.000001,
            value = 42
        };

        // Act
        var toon = ToonSerializer.Serialize(obj);

        // Assert
        Assert.Contains("large: 1000000", toon);
        Assert.Contains("small: 0.000001", toon);
        Assert.Contains("value: 42", toon);
        // Check that scientific notation (e+ or e-) is not used
        Assert.DoesNotContain("e+", toon);
        Assert.DoesNotContain("e-", toon);
        Assert.DoesNotContain("E+", toon);
        Assert.DoesNotContain("E-", toon);
    }
}

/// <summary>
/// Extension methods for test utilities.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Normalizes line endings to LF only (TOON spec requirement).
    /// </summary>
    public static string NormalizeLineEndings(this string input) => input.Replace("\r\n", "\n");
}
