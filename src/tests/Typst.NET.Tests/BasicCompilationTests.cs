using Xunit;

namespace Typst.NET.Tests;

public sealed class BasicCompilationTests
{
    [Fact]
    public void Compile_SimpleDocument_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("= Hello Typst.NET!\n\nThis is a test.");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.True(result.Document.PageCount > 0);
    }

    [Fact]
    public void Compile_InvalidSyntax_ReturnsError()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("#let x = (unclosed");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void RenderPageToSvg_FirstPage_ReturnsSvg()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Test page\n\nContent.");

        // Act
        var svg = result.Document!.RenderPageToSvg(0);

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void RenderAllPagesToSvg_MultiplePages_ReturnsArray()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Page 1\n#pagebreak()\n= Page 2\n#pagebreak()\n= Page 3"
        );

        // Act
        var svgs = result.Document!.RenderAllPagesToSvg();

        // Assert
        Assert.Equal(3, svgs.Length);
        Assert.All(svgs, svg => Assert.Contains("<svg", svg));
    }

    [Fact]
    public void Compile_EmptySource_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal(1, result.Document.PageCount);
    }

    [Fact]
    public void Compile_UnicodeContent_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile(
            "= Unicode Test\n\n"
                + "中文 Japanese 日本語 한국어\n"
                + "Emoji: 🚀 🎉 💻\n"
                + "Math: ∫∞ ≠ ∑ ∏"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_MathFormulas_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile(
            "= Math Test\n\n"
                + "Inline: $a^2 + b^2 = c^2$\n\n"
                + "Block:\n"
                + "$ integral_0^infinity e^(-x^2) dif x = sqrt(pi)/2 $"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_CodeBlock_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile(
            "= Code Test\n\n"
                + "```rust\n"
                + "fn main() {\n"
                + "    println!(\"Hello, typst!\");\n"
                + "}\n"
                + "```"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_Table_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile(
            "= Table Test\n\n"
                + "#table(\n"
                + "  columns: 3,\n"
                + "  [A], [B], [C],\n"
                + "  [1], [2], [3],\n"
                + ")"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_List_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile(
            "= List Test\n\n"
                + "- Item 1\n"
                + "- Item 2\n"
                + "  - Nested 2.1\n"
                + "  - Nested 2.2\n"
                + "- Item 3\n\n"
                + "1. First\n"
                + "2. Second\n"
                + "3. Third"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_MultipleCompilations_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result1 = compiler.Compile("= First");
        using var result2 = compiler.Compile("= Second");
        using var result3 = compiler.Compile("= Third");

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
    }

    [Fact]
    public void Compile_LargeDocument_CrossesBufferThreshold()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        var largeContent =
            "= Large Doc\n" + string.Join("\n#pagebreak()\n", Enumerable.Repeat("Content", 10));

        // Act
        using var result = compiler.Compile(largeContent);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal(10, result.Document.PageCount);
    }

    [Fact]
    public void ThrowIfFailed_OnInvalidSyntax_ThrowsTypstException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("#let x = (unclosed");

        // Act & Assert
        Assert.Throws<TypstException>(() => result.ThrowIfFailed());
    }

    [Fact]
    public void RenderToPdf_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= PDF Test\n\nContent here.");

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        // PDF files start with "%PDF-"
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
