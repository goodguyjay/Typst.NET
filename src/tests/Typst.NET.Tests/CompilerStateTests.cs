using Xunit;

namespace Typst.NET.Tests;

public sealed class CompilerStateTests
{
    [Fact]
    public void Compile_TwiceWithDifferentSource_ProducesIndependentResults()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result1 = compiler.Compile("= First Document\n\nFirst content.");
        using var result2 = compiler.Compile("= Second Document\n\nSecond content.");

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.NotNull(result1.Document);
        Assert.NotNull(result2.Document);

        // Render should produce different content
        var svg1 = result1.Document!.RenderPageToSvg(0);
        var svg2 = result2.Document!.RenderPageToSvg(0);
        Assert.NotEqual(svg1, svg2);
    }

    [Fact]
    public void Compile_SuccessThenError_ErrorDoesNotAffectState()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result1 = compiler.Compile("= Good document.");
        using var result2 = compiler.Compile("#let x = (bad");
        using var result3 = compiler.Compile("= Le Good Document");

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.True(result3.Success); // should recover from error
    }

    [Fact]
    public void Compile_ErrorThenSuccess_SuccessWorks()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result1 = compiler.Compile("#let x = (bad");
        using var result2 = compiler.Compile("= Now good.");

        // Assert
        Assert.False(result1.Success);
        Assert.True(result2.Success); // should succeed after previous error
        Assert.NotNull(result2.Document);
    }

    [Fact]
    public void Compile_AfterCacheReset_StillWorks()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        using var result1 = compiler.Compile("= Before cache reset.");
        Assert.True(result1.Success);

        // Act
        TypstCompiler.ResetCache(TimeSpan.Zero);

        using var result2 = compiler.Compile("= After cache reset.");

        // Assert
        Assert.True(result2.Success);
        Assert.NotNull(result2.Document);
    }

    [Fact]
    public void Compile_MultipleResetCycles_Stable()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act & Assert - multiple resets and compiles
        for (var i = 0; i < 5; i++)
        {
            using var result = compiler.Compile($"= Document {i}");
            Assert.True(result.Success);

            TypstCompiler.ResetCache(TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public void TypstCompiler_MultipleConcurrentInstances_Isolated()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act
        using var compiler1 = new TypstCompiler(tempDir);
        using var compiler2 = new TypstCompiler(tempDir);
        using var compiler3 = new TypstCompiler(tempDir);

        using var result1 = compiler1.Compile("= Compiler 1");
        using var result2 = compiler2.Compile("= Compiler 2");
        using var result3 = compiler3.Compile("= Compiler 3");

        // Assert - all succeed independently
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
    }

    [Fact]
    public void Compile_SameSourceMultipleTimes_Deterministic()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        const string source = "= Test\n\nContent here.";

        // Act - compile same source 10 times
        var results = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            using var result = compiler.Compile(source);
            Assert.True(result.Success);
            results.Add(result.Document!.RenderPageToSvg(0));
        }

        // Assert - all outputs are identical
        var firstRender = results[0];
        Assert.All(results, svg => Assert.Equal(firstRender, svg));
    }

    [Fact]
    public void Compile_AlternatingSimpleAndComplex_BothWork()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        const string simple = "= Simple";
        const string complex =
            "= Complex\n\n"
            + "$integral_0^infinity e^(-x^2) dif x$\n\n"
            + "#table(columns: 3, [A], [B], [C], [1], [2], [3])";

        // Act & Assert - alternate compilations
        for (var i = 0; i < 5; i++)
        {
            using var simpleResult = compiler.Compile(simple);
            Assert.True(simpleResult.Success);
            Assert.NotNull(simpleResult.Document);

            using var complexResult = compiler.Compile(complex);
            Assert.True(complexResult.Success);
            Assert.NotNull(complexResult.Document);
        }
    }

    [Fact]
    public void Compile_VeryLongSession_NoStateCorruption()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act - simulate long-running session
        for (var i = 0; i < 100; i++)
        {
            using var result = compiler.Compile($"= Document {i}\n\nIteration {i}.");
            Assert.True(result.Success, $"Compilation {i} failed");
        }

        // Final compilation to ensure state is still good
        using var finalResult = compiler.Compile("= Final Document\n\nAll iterations complete.");
        Assert.True(finalResult.Success);
    }
}
