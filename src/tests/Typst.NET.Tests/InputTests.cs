using Xunit;

namespace Typst.NET.Tests;

public sealed class InputTests
{
    [Fact]
    public void Compile_WithEmptyInputs_UsesDefaults()
    {
        // Arrange
        // ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
        var options = new TypstCompilerOptions { WorkspaceRoot = Path.GetTempPath(), Inputs = { } };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let title = sys.inputs.at("title", default: "Default Title")
            = #title
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
        var svg = result.Document!.RenderPageToSvg(0);
        Assert.NotNull(svg);
        Assert.NotEmpty(svg);
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void Compile_WithSingleInput_AccessibleInTypst()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["name"] = "Julia" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let name = sys.inputs.at("name")
            Hello, #name!
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
        var svg = result.Document!.RenderPageToSvg(0);
        Assert.NotNull(svg);
        Assert.NotEmpty(svg);
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void Compile_WithNumericStringInput_ConvertsToInt()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["count"] = "42" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let count = int(sys.inputs.count)
            #for i in range(count) [.]
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
        var svg = result.Document!.RenderPageToSvg(0);
        Assert.NotNull(svg);
        Assert.NotEmpty(svg);
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void Compile_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs =
            {
                ["quote"] = "\"Hello\"",
                ["slash"] = "path/to/file",
                ["backslash"] = "C:\\Windows",
                ["newline"] = "line1\nline2",
            },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            Quote: #sys.inputs.quote

            Slash: #sys.inputs.slash

            Backslash: #sys.inputs.backslash
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_WithUnicode_HandlesCorrectly()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs =
            {
                ["chinese"] = "你好世界",
                ["japanese"] = "こんにちは",
                ["emoji"] = "🚀💻🎉",
                ["arabic"] = "مرحبا",
            },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            Chinese: #sys.inputs.chinese

            Japanese: #sys.inputs.japanese

            Emoji: #sys.inputs.emoji

            Arabic: #sys.inputs.arabic
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
        var svg = result.Document!.RenderPageToSvg(0);
        Assert.NotNull(svg);
        Assert.NotEmpty(svg);
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void Compile_WithVeryLongInput_Succeeds()
    {
        // Arrange
        var longText = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet. ", 1000));
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["long_text"] = longText },
        };

        using var compiler = new TypstCompiler(options);
        const string source = "#sys.inputs.long_text";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
        var svg = result.Document!.RenderPageToSvg(0);
        Assert.NotNull(svg);
        Assert.NotEmpty(svg);
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void Compile_InputsControlPageCount_Works()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["pages"] = "5" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let pages = int(sys.inputs.pages)
            #for i in range(pages) [
                = Page #(i + 1)
                #pagebreak()
            ]
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(6, result.Document!.PageCount);
    }

    [Fact]
    public void Compile_WithDifferentInputs_ProducesDifferentOutput()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        var options1 = new TypstCompilerOptions
        {
            WorkspaceRoot = tempDir,
            Inputs = { ["color"] = "red" },
        };

        var options2 = new TypstCompilerOptions
        {
            WorkspaceRoot = tempDir,
            Inputs = { ["color"] = "blue" },
        };

        const string source = "Color: #sys.inputs.color";

        // Act
        using var compiler1 = new TypstCompiler(options1);
        using var result1 = compiler1.Compile(source);

        using var compiler2 = new TypstCompiler(options2);
        using var result2 = compiler2.Compile(source);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);

        var svg1 = result1.Document!.RenderPageToSvg(0);
        var svg2 = result2.Document!.RenderPageToSvg(0);

        Assert.NotEqual(svg1, svg2);
    }

    [Fact]
    public void Compile_InputsInMathMode_Works()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["variable"] = "x" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let var = sys.inputs.variable
            $ #var^2 + 2#var + 1 = 0 $
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_InputsWithWhitespace_Preserved()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["spaced"] = "  leading and trailing  ", ["multispace"] = "word    word" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            [#sys.inputs.spaced]
            [#sys.inputs.multispace]
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_EmptyStringInput_Accessible()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["empty"] = "" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let val = sys.inputs.at("empty", default: "default")
            Value: [#val]
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_InputsWithReservedKeywords_Works()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs =
            {
                ["for"] = "loop",
                ["if"] = "condition",
                ["let"] = "variable",
            },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #sys.inputs.at("for")
            #sys.inputs.at("if")
            #sys.inputs.at("let")
            """;

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_StressTest_1000Compilations_SameInputs()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["value"] = "consistent" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = "#sys.inputs.value";

        // Act & Assert
        for (var i = 0; i < 1000; i++)
        {
            using var result = compiler.Compile(source);
            Assert.True(result.Success, $"Compilation {i} failed");
        }
    }
}
