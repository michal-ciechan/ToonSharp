# ToonSharp

A high-performance, .NET 9 library for serializing and deserializing data in the TOON format - a human-readable, line-oriented data serialization format optimized for LLM contexts.

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## Features

- **TOON v1.4 Specification Support** - Implements the TOON specification with 16 known deviations
- **Performance-Driven** - Built with .NET 9 modern performance features
- **Type-Safe** - Leverages C# 12 features and nullable reference types
- **Strict Mode** - Optional strict validation for production environments
- **Tabular Data** - First-class support for tabular arrays
- **Multiple Delimiters** - Comma, tab, and pipe delimiter support
- **Fully Documented** - Comprehensive XML documentation for IntelliSense

## Installation

```bash
dotnet add package ToonSharp
```

## Quick Start

### Serialization

```csharp
using ToonSharp;

// Simple object
var user = new
{
    id = 123,
    name = "Ada Lovelace",
    active = true
};

var toon = ToonSerializer.Serialize(user);
// Output:
// id: 123
// name: Ada Lovelace
// active: true
```

### Deserialization

```csharp
using ToonSharp;
using System.Text.Json.Nodes;

var toon = """
id: 123
name: Ada Lovelace
active: true
""";

var result = ToonSerializer.Deserialize(toon);
var obj = result.AsObject();

Console.WriteLine(obj["name"]); // "Ada Lovelace"
```

### Strongly-Typed Deserialization

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool Active { get; set; }
}

var user = ToonSerializer.Deserialize<User>(toon);
Console.WriteLine(user.Name); // "Ada Lovelace"
```

## Examples

### Nested Objects

```csharp
var data = new
{
    user = new
    {
        id = 123,
        name = "Ada"
    }
};

var toon = ToonSerializer.Serialize(data);
// Output:
// user:
//   id: 123
//   name: Ada
```

### Primitive Arrays

```csharp
var data = new
{
    tags = new[] { "admin", "developer", "ops" }
};

var toon = ToonSerializer.Serialize(data);
// Output:
// tags[3]: admin,developer,ops
```

### Tabular Data

```csharp
var data = new
{
    products = new[]
    {
        new { sku = "A1", qty = 2, price = 9.99 },
        new { sku = "B2", qty = 1, price = 14.50 }
    }
};

var toon = ToonSerializer.Serialize(data);
// Output:
// products[2]{sku,qty,price}:
//   A1,2,9.99
//   B2,1,14.5
```

### Custom Delimiters

```csharp
var options = new ToonSerializerOptions
{
    Delimiter = ToonDelimiter.Tab
};

var data = new { tags = new[] { "reading", "gaming", "coding" } };
var toon = ToonSerializer.Serialize(data, options);
// Output:
// tags[3	]: reading	gaming	coding
```

### Async Operations

```csharp
// Serialize to stream
await using var stream = File.Create("data.toon");
await ToonSerializer.SerializeAsync(stream, data);

// Deserialize from stream
await using var readStream = File.OpenRead("data.toon");
var result = await ToonSerializer.DeserializeAsync<MyType>(readStream);
```

## Configuration

### ToonSerializerOptions

```csharp
var options = new ToonSerializerOptions
{
    IndentSize = 2,              // Spaces per indentation level (default: 2)
    Delimiter = ToonDelimiter.Comma, // Document delimiter (default: Comma)
    UseLengthMarker = false,     // Include # in array headers (default: false)
    Strict = true                // Enable strict mode (default: true)
};
```

### Strict Mode

When `Strict = true` (default), the parser enforces:

- Array counts must match declared lengths
- Indentation must be exact multiples of `IndentSize`
- Tabs cannot be used for indentation
- Invalid escape sequences cause errors
- Missing colons after keys cause errors
- Blank lines inside arrays/tabular rows cause errors

```csharp
// Strict mode (default)
var strictOptions = new ToonSerializerOptions { Strict = true };

// Non-strict mode (more lenient)
var lenientOptions = new ToonSerializerOptions { Strict = false };
```

## Supported Types

### Primitives
- `string`
- `int`, `long`, `short`, `byte` (and unsigned variants)
- `float`, `double`, `decimal`
- `bool`
- `null`

### Collections
- Arrays (`T[]`)
- `List<T>`
- `IEnumerable<T>`

### Objects
- POCOs (Plain Old CLR Objects)
- Anonymous types
- `Dictionary<string, T>`
- `JsonObject` / `JsonArray` / `JsonNode`

## API Reference

### ToonSerializer

| Method | Description |
|--------|-------------|
| `Serialize<T>(T value, options?)` | Converts value to TOON string |
| `SerializeAsync<T>(Stream, T value, options?, token?)` | Async serialize to stream |
| `Deserialize(string toon, options?)` | Parses TOON to JsonNode |
| `Deserialize<T>(string toon, options?)` | Parses TOON to type T |
| `DeserializeAsync<T>(Stream, options?, token?)` | Async deserialize from stream |
| `TryDeserialize<T>(string, out T?, options?)` | Safe deserialization |

### ToonDelimiter

- `Comma` - Default comma delimiter (`,`)
- `Tab` - Tab delimiter (`\t`)
- `Pipe` - Pipe delimiter (`|`)

## Error Handling

```csharp
try
{
    var result = ToonSerializer.Deserialize(toon);
}
catch (ToonException ex)
{
    Console.WriteLine($"Error at line {ex.LineNumber}: {ex.Message}");
}
```

## Performance

ToonSharp is built with performance in mind:

- Uses `Span<T>` and `ReadOnlySpan<T>` for zero-allocation string operations
- Minimal allocations during parsing
- Efficient `StringBuilder` usage for serialization
- Optimized for .NET 9 runtime improvements

## Specification

This library implements the [TOON Specification v1.4](https://github.com/toon-format/spec/blob/v1.4.0/SPEC.md) (local copy: [SPEC.md](SPEC.md)).

The test fixtures in `ToonSharp.Tests/SpecTests/Specs/` are a direct copy from the official [toon-format/spec](https://github.com/toon-format/spec/tree/main/tests/fixtures) repository.

## Spec Deviations

ToonSharp has 16 known deviations from the official TOON v1.4 specification tests. The following are documented:

### Encode: Hyphen Quoting

Single hyphens and strings starting with "- " are not quoted when they should be to avoid list item ambiguity.

| Test | Input | Expected | Actual |
|------|-------|----------|--------|
| quotes single hyphen as object value | `{ "marker": "-" }` | `marker: "-"` | `marker: -` |
| quotes single hyphen in array | `{ "items": ["-"] }` | `items[1]: "-"` | `items[1]: -` |
| quotes leading-hyphen string in array | `{ "tags": ["a", "- item", "b"] }` | `tags[3]: a,"- item",b` | `tags[3]: a,- item,b` |

### Encode: Null in Tabular Format

Arrays containing null values fall back to list format instead of using tabular format with explicit null.

| Test | Input | Expected | Actual |
|------|-------|----------|--------|
| serializes tabular array with null values | `[{id:1,val:10},{id:2,val:null}]` | `items[2]{id,val}:\n  1,10\n  2,null` | List format |

### Decode: Quoted Keys with Brackets

Keys with brackets in quotes are misinterpreted as array notation.

| Test | Input | Expected | Error |
|------|-------|----------|-------|
| parses field with quoted key containing brackets | `"key[test]"[3]: 1,2,3` | `{"key[test]": [1,2,3]}` | Invalid array length: test |
| parses field with quoted key starting with bracket | `"[index]": 5` | `{"[index]": 5}` | Crash |

### Decode: Quoted Field Names in Tabular

Tabular headers with quoted field names containing special characters fail to parse.

| Test | Input | Expected |
|------|-------|----------|
| parses tabular array with quoted field names | `items[2]{"order:id","full name"}:\n  1,Ada\n  2,Bob` | `{"items": [{...}, {...}]}` |

### Decode: Blank Line Handling

Blank lines after arrays are incorrectly treated as part of the array.

| Test | Input | Expected |
|------|-------|----------|
| allows blank line after primitive array | `tags[2]: a,b\n\nother: value` | `{"tags": ["a","b"], "other": "value"}` |

### Decode: Nested Arrays in List Items

Inline array syntax within list items creates a string key instead of nested array.

| Test | Input | Expected | Actual |
|------|-------|----------|--------|
| parses list-form array with inline arrays | `items:\n- tags[3]: a,b,c` | `{"items": [{"tags": ["a","b","c"]}]}` | Key becomes `"tags[3]"` |

### Decode: Delimiter Inheritance in List Items

Nested arrays and object values in list items don't properly inherit or follow delimiter rules.

| Test | Input | Expected |
|------|-------|----------|
| parses nested arrays inside list items with default comma delimiter | `items[1\t]:\n  - tags[3]: a,b,c` | Nested array uses comma |
| object values in list items follow document delimiter | `items[2\t]:\n  - status: a,b` | Value is `"a,b"` not parsed as array |
| object values with comma must be quoted | `items[2]:\n  - status: "a,b"` | Value is `"a,b"` |

### Decode: Negative Leading-Zero Numbers

Negative numbers with leading zeros are parsed as numbers instead of strings.

| Test | Input | Expected | Actual |
|------|-------|----------|--------|
| negative with leading zeros stays string | `-05` | `"-05"` (string) | `-5` (number) |
| treats negative leading-zeros in array as strings | `nums[2]: -05,-007` | `["-05", "-007"]` | `[-5, -7]` |

### Decode: Root Primitives

Root-level quoted strings with backslashes and empty documents have issues.

| Test | Input | Expected | Actual |
|------|-------|----------|--------|
| parses quoted string with backslash as root value | `"C:\\Users\\path"` | `"C:\\Users\\path"` | Error: Missing colon after key |
| parses empty document as empty object | `` (empty) | `{}` | Error: Empty input |

### Decode: Unterminated String Detection

Unterminated strings don't throw an error in all contexts.

| Test | Input | Expected |
|------|-------|----------|
| throws on unterminated string | `"unterminated` | Should throw error |

### Encode: Floating-Point Precision

Large integers and repeating decimals lose precision during serialization.

| Test | Input | Expected | Actual |
|------|-------|----------|--------|
| encodes MAX_SAFE_INTEGER | `9007199254740991` | `9007199254740991` | `9007199254740990` |
| encodes repeating decimal with full precision | `0.3333333333333333` | `0.3333333333333333` | `0.333333333333333` |

## Contributing

Contributions are welcome! Please ensure:

1. All tests pass (`dotnet test`)
2. Code follows .NET conventions
3. XML documentation is complete
4. Tests cover changes

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

Built with .NET 9 following modern C# best practices and the System.Text.Json design patterns.
This is a port of https://github.com/johannschopplich/toon to .NET9

---

Made with ❤️ for the .NET community
