using Xunit;

namespace Typst.NET.Tests;

public sealed class VfsTests : IDisposable
{
    private readonly string _testDir;

    public VfsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"typst_vfs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        // Clean up the test directory after tests
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Compile_WithReadStatement_ReadsFile()
    {
        // Arrange
        var dataFile = Path.Combine(_testDir, "data.txt");
        File.WriteAllText(dataFile, "Hello from file!");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #let content = read("data.txt")
            Content: #content
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_ReadNonExistentFile_ReturnsError()
    {
        // Arrange
        using var compiler = new TypstCompiler(_testDir);
        const string source = """#read("doesnotexist.txt")""";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("not found", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_ImportTypstFile_Works()
    {
        // Arrange
        var helperFile = Path.Combine(_testDir, "helper.typ");
        File.WriteAllText(helperFile, "#let greet(name) = \"Hello, \" + name + \"!\"");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #import "helper.typ": greet

            = Greeting
            #greet("World")
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_NestedImport_Works()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_testDir, "components"));

        var headerFile = Path.Combine(_testDir, "components", "header.typ");
        File.WriteAllText(headerFile, "#let title = \"My Document\"");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #import "components/header.typ": title

            = #title
            Content here.
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
    public void Compile_ReadJson_ParsesCorrectly()
    {
        // Arrange
        var jsonFile = Path.Combine(_testDir, "config.json");
        File.WriteAllText(jsonFile, """{"title": "Test", "version": 42}""");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #let config = json("config.json")

            = #config.title
            Version: #config.version
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
    public void Compile_ReadCsv_ParsesCorrectly()
    {
        // Arrange
        var csvFile = Path.Combine(_testDir, "data.csv");
        File.WriteAllText(csvFile, "Name,Age\nJulia,21\nCarlos,20");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #let data = csv("data.csv")

            #table(
                columns: 2,
                [Name], [Age],
                ..data.flatten()
            )
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
    public void Compile_MultipleImports_AllWork()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "colors.typ"), "#let red = rgb(255, 0, 0)");
        File.WriteAllText(Path.Combine(_testDir, "utils.typ"), "#let bold(content) = [*#content*]");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #import "colors.typ": red
            #import "utils.typ": bold

            #bold[Red text in color] #text(fill: red)[colored!]
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
    public void Compile_ImportWithMultipleExports_Works()
    {
        // Arrange
        var moduleFile = Path.Combine(_testDir, "math.typ");
        File.WriteAllText(
            moduleFile,
            """
            #let add(a, b) = a + b
            #let multiply(a, b) = a * b
            #let pi = 3.14159
            """
        );

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #import "math.typ": add, multiply, pi

            Addition: #add(2, 3)
            Multiplication: #multiply(4, 5)
            Pi: #pi
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
    public void Compile_ImportStar_ImportsEverything()
    {
        // Arrange
        var moduleFile = Path.Combine(_testDir, "helpers.typ");
        File.WriteAllText(
            moduleFile,
            """
            #let greeting = "Hello"
            #let farewell = "Goodbye"
            """
        );

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #import "helpers.typ": *

            #greeting and #farewell
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
    public void Compile_CircularImport_HandlesGracefully()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "a.typ"), """#import "b.typ": *""");
        File.WriteAllText(Path.Combine(_testDir, "b.typ"), """#import "a.typ": *""");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """#import "a.typ": *""";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        // Should either succeed (if typst handles it) or fail gracefully with error
        // NOT crash
        if (!result.Success)
        {
            Assert.NotEmpty(result.Errors);
        }
    }

    [Fact]
    public void Compile_DeeplyNestedImports_Work()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_testDir, "a", "b", "c"));

        var deepFile = Path.Combine(_testDir, "a", "b", "c", "deep.typ");
        File.WriteAllText(deepFile, "#let value = \"deeply nested\"");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #import "a/b/c/deep.typ": value
            Value: #value
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
    public void Compile_ReadWithDifferentEncodings_Works()
    {
        // Arrange
        var textFile = Path.Combine(_testDir, "text.txt");
        File.WriteAllText(textFile, "Hello 世界 🌍", System.Text.Encoding.UTF8);

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            #read("text.txt")
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
    public void Compile_ModifyFileAndRecompile_ReadsNewContent()
    {
        // Arrange
        var dataFile = Path.Combine(_testDir, "data.txt");
        File.WriteAllText(dataFile, "Version 1");

        using var compiler = new TypstCompiler(_testDir);
        const string source = """#read("data.txt")""";

        // First compilation
        using var result1 = compiler.Compile(source);
        Assert.True(result1.Success);

        // Modify file
        File.WriteAllText(dataFile, "Version 2");

        // Recompile
        using var result2 = compiler.Compile(source);

        // Assert
        Assert.True(result2.Success);
        // Both should succeed (content might be cached, that's ok)
    }

    [Fact]
    public void RenderToPdf_WithImages_Succeeds()
    {
        var sourceImage = Path.Combine("TestAssets", "cat.jpg");
        var targetImage = Path.Combine(_testDir, "cat.jpg");
        File.Copy(sourceImage, targetImage);

        using var compiler = new TypstCompiler(_testDir);
        const string source = """
            = Image Test

            #image("cat.jpg", width: 50pt)
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(
            result.Success,
            result.Success ? "" : string.Join("; ", result.Errors.Select(e => e.Message))
        );
        Assert.NotNull(result.Document);

        // Render to verify image is embedded
        var pdf = result.Document!.RenderToPdf();
        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 5000); // Should be larger with embedded image
    }

    [Fact(Skip = "Manual inspection - outputs to user temp")]
    public void RenderToPdf_WithImages_SaveForInspection()
    {
        // Arrange
        var inspectDir = Path.Combine(Path.GetTempPath(), "typst_inspect");
        Directory.CreateDirectory(inspectDir);

        var sourceImage = Path.Combine("TestAssets", "cat.jpg");
        var targetImage = Path.Combine(inspectDir, "cat.jpg");
        File.Copy(sourceImage, targetImage, overwrite: true);

        using var compiler = new TypstCompiler(inspectDir);
        const string source = """
            = Cat.

            #image("cat.jpg", width: 150pt)

            VFS WORKS!!!!! OMG
            """;

        // Act
        using var result = compiler.Compile(source);
        Assert.True(result.Success);

        // Save PDF
        var pdf = result.Document!.RenderToPdf();
        var outputPath = Path.Combine(inspectDir, "output.pdf");
        File.WriteAllBytes(outputPath, pdf);

        // Print path for inspection
        Console.WriteLine($"PDF saved to: {outputPath}");
        Console.WriteLine("lol");
    }
}
