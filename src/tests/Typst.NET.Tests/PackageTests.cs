using Xunit;

namespace Typst.NET.Tests;

public sealed class PackageTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _packageRoot;
    private readonly string _workspace;

    public PackageTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"typst_pkg_{Guid.NewGuid():N}");
        _packageRoot = Path.Combine(_testRoot, "packages");
        _workspace = Path.Combine(_testRoot, "workspace");
        Directory.CreateDirectory(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Fact]
    public void Compile_WithLocalPackage_Works()
    {
        // Arrange
        CreatePackage("preview", "mylib", "1.0.0", "#let greeting = \"Hello from package!\"");

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/mylib:1.0.0": greeting
            #greeting
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    [Fact]
    public void Compile_PackageNotFound_ReturnsError()
    {
        // Arrange
        Directory.CreateDirectory(_packageRoot);

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """#import "@preview/nonexistent:1.0.0": *""";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Compile_WrongVersion_ReturnsError()
    {
        // Arrange
        CreatePackage("preview", "lib", "1.0.0", "#let x = 1");

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """#import "@preview/lib:2.0.0": *"""; // wrong version

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public void Compile_PackageWithNestedFiles_Works()
    {
        // Arrange
        var pkgDir = CreatePackage("preview", "utils", "1.0.0", """#import "src/math.typ": *""");

        var srcDir = Path.Combine(pkgDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "math.typ"), "#let add(a, b) = a + b");

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/utils:1.0.0": add
            Result: #add(2, 3)
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    [Fact]
    public void Compile_MultiplePackages_AllWork()
    {
        // Arrange
        CreatePackage("preview", "colors", "1.0.0", "#let red = rgb(255, 0, 0)");
        CreatePackage("preview", "utils", "1.0.0", "#let bold(x) = [*#x*]");

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/colors:1.0.0": red
            #import "@preview/utils:1.0.0": bold

            #bold[Red text] #text(fill: red)[is red]
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    [Fact]
    public void Compile_PackageImportsStar_Works()
    {
        // Arrange
        CreatePackage(
            "preview",
            "math",
            "1.0.0",
            """
            #let add(a, b) = a + b
            #let multiply(a, b) = a * b
            #let pi = 3.14159
            """
        );

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/math:1.0.0": *

            #add(1, 2)
            #multiply(3, 4)
            #pi
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    [Fact]
    public void Compile_WithoutPackagePath_CannotUsePackages()
    {
        // Arrange
        CreatePackage("preview", "lib", "1.0.0", "#let x = 1");

        // NO package path configured
        using var compiler = new TypstCompiler(_workspace);
        const string source = """#import "@preview/lib:1.0.0": *""";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Compile_PackageWithDependencies_Works()
    {
        // Arrange - create two packages where one depends on the other
        CreatePackage("preview", "base", "1.0.0", "#let value = 42");

        CreatePackage(
            "preview",
            "wrapper",
            "1.0.0",
            """
            #import "@preview/base:1.0.0": value
            #let doubled = value * 2
            """
        );

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/wrapper:1.0.0": doubled
            Result: #doubled
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    [Fact]
    public void Compile_DifferentNamespaces_BothWork()
    {
        // Arrange
        CreatePackage("preview", "lib1", "1.0.0", "#let a = 1");
        CreatePackage("custom", "lib2", "1.0.0", "#let b = 2");

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/lib1:1.0.0": a
            #import "@custom/lib2:1.0.0": b
            Sum: #(a + b)
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    [Fact]
    public void Compile_PackageAndWorkspaceFiles_BothWork()
    {
        // Arrange
        CreatePackage("preview", "lib", "1.0.0", "#let pkg_val = \"from package\"");

        File.WriteAllText(
            Path.Combine(_workspace, "local.typ"),
            "#let local_val = \"from workspace\""
        );

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _workspace,
            PackagePath = _packageRoot,
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #import "@preview/lib:1.0.0": pkg_val
            #import "local.typ": local_val

            #pkg_val and #local_val
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
    }

    /// <summary>
    /// Helper to create a package with manifest
    /// </summary>
    private string CreatePackage(string ns, string name, string version, string libContent)
    {
        var pkgDir = Path.Combine(_packageRoot, ns, name, version);
        Directory.CreateDirectory(pkgDir);

        // Write manifest
        File.WriteAllText(
            Path.Combine(pkgDir, "typst.toml"),
            $"""
            [package]
            name = "{name}"
            version = "{version}"
            entrypoint = "lib.typ"
            """
        );

        // Write lib
        File.WriteAllText(Path.Combine(pkgDir, "lib.typ"), libContent);

        return pkgDir;
    }
}
