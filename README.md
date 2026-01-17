# Typst.NET

A .NET wrapper for the Typst document compiler. Provides safe, high performance access to Typst's compilation engine with full support for the virtual file system, custom fonts, and offline packages.

## Features

- **PDF and SVG rendering** - Compile Typst documents to PDF or render individual pages as SVG
- **Custom inputs** - Pass variables to Typst via a type-safe API
- **Virtual file system** - Support for imports, images, and file reading
- **Offline packages** - Use `@preview` packages without network access
- **Custom fonts** - Load fonts from specified directories
- **Comprehensive diagnostics** - Error messages with line/column information
- **Memory safe** - Proper resource management and disposal patterns
- **Zero-copy** - Optimized for performance with minimal allocations

## Installation
```bash
dotnet add package TypstNET
```

**Platform support:**
- Windows (x64) ✓
- Linux (x64) ✓  
- macOS - Not yet available (contributions welcome)

## Quick Start
```csharp
using Typst.NET;

// Basic compilation
using var compiler = new TypstCompiler(workspaceRoot: ".");
using var result = compiler.Compile("= Hello World");

if (result.Success)
{
    var pdf = result.Document.RenderToPdf();
    File.WriteAllBytes("output.pdf", pdf);
}
else
{
    foreach (var error in result.Errors)
        Console.WriteLine($"{error.Message} at {error.Location?.Line}:{error.Location?.Column}");
}
```

## Advanced Usage

### Custom Inputs
```csharp
var options = new TypstCompilerOptions
{
    WorkspaceRoot = ".",
    Inputs = new()
    {
        ["author"] = "Jay",
        ["date"] = "2025-04-02",
        ["draft"] = "false"
    }
};

using var compiler = new TypstCompiler(options);
using var result = compiler.Compile("#sys.inputs.author wrote this on #sys.inputs.date");
```

### Images and Imports
```csharp
// Place files in workspace root
// workspace/
//   components/header.typ
//   logo.png

var options = new TypstCompilerOptions { WorkspaceRoot = "./workspace" };
using var compiler = new TypstCompiler(options);

const string source = """
    #import "components/header.typ": *
    
    = Document Title
    #image("logo.png", width: 100pt)
    """;

using var result = compiler.Compile(source);
```

### Offline Packages
```csharp
var options = new TypstCompilerOptions
{
    WorkspaceRoot = ".",
    PackagePath = "./packages"  // Directory containing @preview packages
};

using var compiler = new TypstCompiler(options);

// Package structure: packages/preview/name/version/
// Example: packages/preview/cetz/0.1.0/lib.typ
const string source = """#import "@preview/cetz:0.1.0": *""";
```

### Custom Fonts
```csharp
var options = new TypstCompilerOptions
{
    WorkspaceRoot = ".",
    CustomFontPaths = ["./fonts", "./assets/typography"],
    IncludeSystemFonts = true
};

using var compiler = new TypstCompiler(options);
```

### SVG Rendering
```csharp
using var result = compiler.Compile(source);

if (result.Success)
{
    for (var i = 0; i < result.Document.PageCount; i++)
    {
        var svg = result.Document.RenderPageToSvg(i);
        File.WriteAllText($"page-{i}.svg", svg);
    }
}
```

## Architecture

Typst.NET provides production-grade bindings with:

- **Type safety** - Strongly typed API with proper error handling
- **Resource management** - Automatic cleanup via `IDisposable`
- **Performance** - Stack allocation and array pooling for large strings
- **Isolation** - Minimal coupling to Typst internals, reducing breaking changes
- **Security** - Path traversal protection for file operations

The library wraps Typst's Rust compiler via FFI while maintaining .NET idioms and safety guarantees.

## Building from Source

### Prerequisites

- .NET 10
- Rust toolchain (for native lib)

### Build Steps
```bash
# Build Rust library
cd typst-net-core
cargo build --release

# Copy native library to runtimes folder
# Windows: copy target/release/typst_net_core.dll to src/Typst.NET/runtimes/win-x64/native/
# Linux:   copy target/release/libtypst_net_core.so to src/Typst.NET/runtimes/linux-x64/native/

# Build .NET library
cd ../src/Typst.NET
dotnet build

# Run tests
cd ../../tests/Typst.NET.Tests
dotnet test
```

## Limitations

- **macOS**: Native library not yet available. Requires Rust cross-compilation setup or macOS build machine.
- **Package downloads**: This library does not download packages from the internet. For offline package support, manually populate the package directory with the required structure.

## Contributing

Contributions are welcome, especially:

- macOS support (native library builds)
- Additional test coverage
- Documentation improvements
- Bug reports and fixes

## License

MIT License

## Acknowledgments

Built on the [Typst](https://github.com/typst/typst) document compiler.