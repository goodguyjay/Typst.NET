using Xunit;

namespace Typst.NET.Tests;

public sealed class FontTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _fontDir;

    public FontTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"typst_fonts_{Guid.NewGuid():N}");
        _fontDir = Path.Combine(_testDir, "fonts");
        Directory.CreateDirectory(_fontDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Compile_WithCustomFontPath_DoesNotCrash()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _testDir,
            CustomFontPaths = [_fontDir],
            IncludeSystemFonts = false,
        };

        // Act
        using var compiler = new TypstCompiler(options);
        const string source = "= Hello Custom Fonts";
        using var result = compiler.Compile(source);

        // Assert - should compile even with no fonts (will use embedded)
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_WithMultipleFontPaths_Works()
    {
        // Arrange
        var fontDir1 = Path.Combine(_testDir, "fonts1");
        var fontDir2 = Path.Combine(_testDir, "fonts2");
        Directory.CreateDirectory(fontDir1);
        Directory.CreateDirectory(fontDir2);

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _testDir,
            CustomFontPaths = [fontDir1, fontDir2],
            IncludeSystemFonts = true,
        };

        using var compiler = new TypstCompiler(options);
        const string source = "= Multiple Font Paths Test";
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_WithEmptyFontPaths_UsesSystemFonts()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _testDir,
            CustomFontPaths = [],
            IncludeSystemFonts = true,
        };

        using var compiler = new TypstCompiler(options);
        const string source = "= System Fonts Test";
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_NoSystemFontsNoCustom_UsesEmbedded()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _testDir,
            CustomFontPaths = [],
            IncludeSystemFonts = false,
        };

        using var compiler = new TypstCompiler(options);
        const string source = "= Embedded Fonts Only";
        using var result = compiler.Compile(source);

        // Assert - embedded fonts should still work
        Assert.True(result.Success);
    }

    [Fact]
    public void Compile_NonExistentFontPath_IgnoresGracefully()
    {
        // Arrange
        var nonExistent = Path.Combine(_testDir, "does_not_exist");

        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = _testDir,
            CustomFontPaths = [nonExistent],
            IncludeSystemFonts = true,
        };

        using var compiler = new TypstCompiler(options);
        const string source = "= Graceful Handling";
        using var result = compiler.Compile(source);

        // Assert - should still work, just ignores bad path
        Assert.True(result.Success);
    }
}
