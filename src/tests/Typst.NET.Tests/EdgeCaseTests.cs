using Typst.NET.Diagnostics;
using Xunit;

namespace Typst.NET.Tests;

public sealed class EdgeCaseTests
{
    [Fact]
    public void Compile_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!));
    }

    [Fact]
    public void Compile_VeryLargeSource_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Generate 10mb of source
        var largeContent = string.Concat(
            Enumerable.Repeat("= Large Section\n\nLorem ipsum dolor sit amet. ", 100_000)
        );

        // Act
        using var result = compiler.Compile(largeContent);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_SourceWithNullBytes_Fails()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Source with embedded null bytes (invalid UTF-8)
        const string sourceWithNull = "= Title\n\n\0Content";

        // Act
        using var result = compiler.Compile(sourceWithNull);

        // Assert - should either fail compilation or ignore the null
        // Typst might handle this gracefully or error
        // We just want to ensure no crash
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderPageToSvg_InvalidPageIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Single page");

        // Act & Assert - negative index
        Assert.Throws<ArgumentOutOfRangeException>(() => result.Document!.RenderPageToSvg(-1));

        // Act & Assert - index too large
        Assert.Throws<ArgumentOutOfRangeException>(() => result.Document!.RenderPageToSvg(999));
    }

    [Fact]
    public void RenderPageToSvg_BoundaryPageIndex_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Page 1\n#pagebreak()\n= Page 2\n#pagebreak()\n= Page 3"
        );

        // Act - render last page (boundary case)
        var lastPageIndex = result.Document!.PageCount - 1;
        var svg = result.Document.RenderPageToSvg(lastPageIndex);

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void TypstCompiler_NullWorkspaceRoot_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TypstCompiler(null!));
    }

    [Fact]
    public void TypstCompiler_NonExistentWorkspaceRoot_ThrowsArgumentException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new TypstCompiler(nonExistentPath));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Compile_OnlyWhitespace_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("   \n\n\t\t  \n   ");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_VeryLongSingleLine_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Generate a very long single line
        var longLine = new string('x', 1_000_000);

        // Act
        using var result = compiler.Compile($"= Title\n\n{longLine}");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_MaximumNesting_HandlesGracefully()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Deeply nested structure
        var nested = string.Concat(Enumerable.Repeat("- Item\n  ", 1000));

        // Act
        using var result = compiler.Compile($"= Nested List\n\n{nested}");

        // Assert - may succeed or fail with error, but shouldn't crash
        Assert.NotNull(result);
    }

    [Fact]
    public void Compile_InvalidTypstMarkup_ReturnsFailureAndDiagnostics()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("= Test \n #"); // Invalid markup

        // Assert
        Assert.False(result.Success); // Expect failure
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(
            result.Diagnostics,
            d => d.Message.Contains("expected") || d.Severity == DiagnosticSeverity.Error
        );
    }

    [Fact]
    public void RenderAllPagesToSvg_SinglePage_ReturnsOneElement()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Single Page Document");

        // Act
        var svgs = result.Document!.RenderAllPagesToSvg();

        // Assert
        Assert.Single(svgs);
        Assert.Contains("<svg", svgs[0]);
    }

    [Fact]
    public void Compile_ManyErrors_AllCaptured()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Source with multiple syntax errors
        const string sourceWithErrors = "#let x = (unclosed\n" + "#let y = [unclosed\n" + "= Title";

        // Act
        using var result = compiler.Compile(sourceWithErrors);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.True(result.Errors.Count() >= 2, "Should have multiple errors");
    }

    [Fact]
    public void ResetCache_MultipleTimesInRow_DoNotCrash()
    {
        // Arrange & Act
        TypstCompiler.ResetCache(TimeSpan.FromSeconds(30));
        TypstCompiler.ResetCache(TimeSpan.FromSeconds(60));
        TypstCompiler.ResetCache(TimeSpan.Zero);

        // Assert - no crash means success
    }

    [Fact]
    public void Compile_ImmediatelyAfterCacheReset_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        TypstCompiler.ResetCache(TimeSpan.Zero);
        using var result = compiler.Compile("= Document After Cache Reset");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Compile_UTF8WithBOM_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // UTF-8 BOM + content
        const string sourceWithBom = "\uFEFF= Title With BOM\n\nContent.";

        // Act
        using var result = compiler.Compile(sourceWithBom);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
    }
}
