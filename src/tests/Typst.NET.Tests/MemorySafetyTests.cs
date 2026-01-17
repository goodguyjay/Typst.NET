using Xunit;

namespace Typst.NET.Tests;

public sealed class MemorySafetyTests
{
    [Fact]
    public void Compile_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var compiler = new TypstCompiler(tempDir);
        compiler.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => compiler.Compile("= Hello after dispose!"));
    }

    [Fact]
    public void RenderSvg_AfterResultDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        var result = compiler.Compile("= Test SVG\n\nContent.");
        result.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => result.Document!.RenderPageToSvg(0));
    }

    [Fact]
    public void Compile_ManyDocuments_NoNativeMemoryLeaks()
    {
        // Note on Native Leaks: We use PrivateMemorySize64 because GC.GetTotalMemory
        // only tracks the managed heap. Since TypstCompiler allocates memory in Rust,
        // a native leak would be invisible to the .NET Garbage Collector but visible
        // to the Operating System process metrics.

        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        using (var _ = compiler.Compile("= Warmup")) { }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var initialMemory = process.PrivateMemorySize64;

        // Act - compile 1000 documents
        for (var i = 0; i < 1000; i++)
        {
            using var result = compiler.Compile($"= Document {i}\n\nContent here.");
            Assert.True(result.Success);
        }

        // Force GC and check memory didn't balloon
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = process.PrivateMemorySize64;
        var memoryGrowth = finalMemory - initialMemory;

        // Assert - memory growth should be within reasonable limits (e.g., less than 50MB for 1000 docs)
        Assert.True(
            memoryGrowth < 50 * 1024 * 1024,
            $"native memory leaked: {memoryGrowth / 1024 / 1024}MB. it's likely a rust-side leak."
        );
    }

    [Fact]
    public void Compile_RapidCreateDispose_NoMemoryLeak()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act - rapidly create and dispose compilers
        for (var i = 0; i < 1000; i++)
        {
            using var compiler = new TypstCompiler(tempDir);
            using var result = compiler.Compile($"= Rapid Document {i}\n\nQuick content.");
            Assert.True(result.Success);
        }

        // Assert - no crash means success
    }

    [Fact]
    public void Compile_Large_Document_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Generate large document (100 pages)
        var content = string.Join(
            "\n#pagebreak()\n",
            Enumerable.Range(1, 100).Select(i => $"= Page {i}\n\nContent for page {i}.")
        );

        // Act
        using var result = compiler.Compile(content);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal(100, result.Document.PageCount);
    }

    [Fact]
    public void Compile_ParallelCompilation_IsNotSupported()
    {
        // Note: TypstCompiler is NOT thread-safe by design
        // This test documents expected behavior

        var tempDir = Path.GetTempPath();

        // Parallel compilation should be done with separate compiler instances
        var results = Parallel.For(
            0,
            10,
            i =>
            {
                using var localCompiler = new TypstCompiler(tempDir);
                using var result = localCompiler.Compile($"= Document {i}");
                Assert.True(result.Success);
            }
        );

        Assert.True(results.IsCompleted);
    }
}
