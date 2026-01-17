using Xunit;

namespace Typst.NET.Tests;

public sealed class IntegrationTests
{
    [Fact]
    public void Workflow_CompileAndRenderMultipleFormats()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        const string source = "= Integration Test\n\nMultiple pages.\n#pagebreak()\n= Page 2";

        // Act - full workflow
        using var result = compiler.Compile(source);
        Assert.True(result.Success);

        var firstPageSvg = result.Document!.RenderPageToSvg(0);
        var secondPageSvg = result.Document.RenderPageToSvg(1);
        var allPages = result.Document.RenderAllPagesToSvg();

        // Assert - all renders work
        Assert.NotEmpty(firstPageSvg);
        Assert.NotEmpty(secondPageSvg);
        Assert.Equal(2, allPages.Length);
        Assert.Equal(firstPageSvg, allPages[0]);
        Assert.Equal(secondPageSvg, allPages[1]);
    }

    [Fact]
    public void Workflow_ErrorRecovery_CanRecoverFromBadCompilation()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act - bad -> good -> bad -> good
        using var bad1 = compiler.Compile("#let x = (bad");
        Assert.False(bad1.Success);

        using var good1 = compiler.Compile("= Good Document 1");
        Assert.True(good1.Success);

        using var bad2 = compiler.Compile("#let y = [unclosed");
        Assert.False(bad2.Success);

        using var good2 = compiler.Compile("= Good Document 2");
        Assert.True(good2.Success);

        // Assert - can render after recovery
        var svg = good2.Document!.RenderPageToSvg(0);
        Assert.NotEmpty(svg);
    }

    [Fact]
    public async Task Workflow_MultipleCompilersInParallel()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var cts = TestContext.Current.CancellationToken;

        // Act - run 10 compilers in parallel
        var tasks = Enumerable
            .Range(0, 10)
            .Select(i =>
                Task.Run(
                    () =>
                    {
                        using var compiler = new TypstCompiler(tempDir);
                        using var result = compiler.Compile($"= Parallel {i}");
                        Assert.True(result.Success);
                        var svg = result.Document!.RenderPageToSvg(0);
                        Assert.NotEmpty(svg);
                    },
                    cts
                )
            );

        // Assert - all complete successfully
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Workflow_LongRunningProcess_NoMemoryLeaks()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        // Warm-up
        using (var _ = compiler.Compile("= Warm-up")) { }
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var initialMemory = process.PrivateMemorySize64;

        // Act - simulate 5 minutes of work (500 compilations)
        for (var i = 0; i < 500; i++)
        {
            using var result = compiler.Compile($"= Document {i}\n\n#pagebreak()\n= Page 2");
            Assert.True(result.Success);

            // Render both pages
            var svg1 = result.Document!.RenderPageToSvg(0);
            var svg2 = result.Document!.RenderPageToSvg(1);
            Assert.NotEmpty(svg1);
            Assert.NotEmpty(svg2);

            // Periodic cache reset
            if (i % 100 == 0)
                TypstCompiler.ResetCache(TimeSpan.FromMinutes(5));
        }

        // Assert - memory is reasonably stable
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = process.PrivateMemorySize64;
        var memoryGrowth = finalMemory - initialMemory;

        Assert.True(
            memoryGrowth < 100 * 1024 * 1024,
            $"Memory leaked: {memoryGrowth / 1024 / 1024}MB after 500 compilations"
        );
    }

    [Fact]
    public void Workflow_DisposePatternsCorrect()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act & Assert - various dispose patterns
        // Pattern 1: explicit dispose
        var compiler1 = new TypstCompiler(tempDir);
        var result1 = compiler1.Compile("= Dispose Pattern 1");
        Assert.True(result1.Success);
        result1.Dispose();
        compiler1.Dispose();

        // Pattern 2: using statement
        // ReSharper disable once ConvertToUsingDeclaration
        using (var compiler2 = new TypstCompiler(tempDir))
        {
            using var result2 = compiler2.Compile("= Test");
            Assert.True(result2.Success);
        }

        // Pattern 3: nested using
        using var compiler3 = new TypstCompiler(tempDir);
        using var result3 = compiler3.Compile("= Test");
        Assert.True(result3.Success);
    }

    [Fact]
    public void Workflow_CacheResetUnderLoad_Stable()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act - compile and reset in tight loop
        for (var i = 0; i < 50; i++)
        {
            using var result = compiler.Compile($"= Doc {i}");
            Assert.True(result.Success);

            // Reset cache every other iteration
            if (i % 2 == 0)
                TypstCompiler.ResetCache(TimeSpan.Zero);
        }

        // Final compilation to ensure stability
        using var final = compiler.Compile("= Final");
        Assert.True(final.Success);
    }

    [Fact]
    public void Compile_WithLocalPackagePath_ResolvesCorrectly()
    {
        // Arrange
        var testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var pkgRoot = Path.Combine(testRoot, "my_packages");
        var workspace = Path.Combine(testRoot, "project");

        var pkgVersionDir = Path.Combine(pkgRoot, "preview", "testpkg", "1.0.0");
        Directory.CreateDirectory(pkgVersionDir);
        Directory.CreateDirectory(workspace);

        // Write the manifest and the lib
        File.WriteAllText(
            Path.Combine(pkgVersionDir, "typst.toml"),
            """
                    [package]
                    name = "testpkg"
                    version = "1.0.0"
                    entrypoint = "lib.typ"
            """
        );

        File.WriteAllText(
            Path.Combine(pkgVersionDir, "lib.typ"),
            "#let val = \"Hello from C# Package Test\""
        );

        var options = new TypstCompilerOptions { WorkspaceRoot = workspace, PackagePath = pkgRoot };

        using var compiler = new TypstCompiler(options);
        const string source = """#import "@preview/testpkg:1.0.0": val; #val""";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success, result.Errors.FirstOrDefault()?.Message);
    }

    [Fact]
    public void Compile_WithError_ReportsCorrectLineAndColumn()
    {
        // Arrange
        using var compiler = new TypstCompiler(Path.GetTempPath());
        // Error is on Line 2
        const string source = "= Hello\n#let x = (unclosed";

        // Act
        using var result = compiler.Compile(source);

        // Assert
        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);

        Assert.NotNull(error.Location);
        var loc = error.Location.Value;

        Assert.Equal(2u, loc.Line);
        Assert.True(loc.Column > 0);
    }
}
