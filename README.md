# ToonSharp

A high-performance, .NET 9 library for serializing and deserializing data in the TOON format - a human-readable, line-oriented data serialization format optimized for LLM contexts.

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## Features

- **Full TOON v1.2 Specification Support** - Complete implementation of the TOON specification
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

This library implements the [TOON Specification v1.2](SPEC.md).

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
