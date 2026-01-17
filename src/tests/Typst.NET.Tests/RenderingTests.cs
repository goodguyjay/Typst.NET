using Xunit;

namespace Typst.NET.Tests;

public sealed class RenderingTests
{
    [Fact]
    public void RenderPageToSvg_SamePageTwice_ReturnsSameContent()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Test Page\n\nContent here.");

        // Act
        var svg1 = result.Document!.RenderPageToSvg(0);
        var svg2 = result.Document!.RenderPageToSvg(0);

        // Assert
        Assert.Equal(svg1, svg2);
    }

    [Fact]
    public void RenderAllPagesToSvg_VerifyPageOrder()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Page One\n" + "#pagebreak()\n" + "= Page Two\n" + "#pagebreak()\n" + "= Page Three"
        );

        // Act
        var allPages = result.Document!.RenderAllPagesToSvg();
        var page0Individual = result.Document!.RenderPageToSvg(0);
        var page2Individual = result.Document!.RenderPageToSvg(2);

        // Assert (order preserved, content matches)
        Assert.Equal(3, allPages.Length);
        Assert.Equal(page0Individual, allPages[0]);
        Assert.Equal(page2Individual, allPages[2]);
    }

    [Fact]
    public void RenderPageToSvg_WithComplexMath_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Complex Math\n\n"
                + "$ integral_0^infinity e^(-x^2) dif x = sqrt(pi)/2 $\n\n"
                + "$ sum_(n=1)^infinity 1/n^2 = pi^2/6 $"
        );

        // Act
        var svg = result.Document!.RenderPageToSvg(0);

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
        // Great heuristic, Jay! I'm sure this is the best way to validate!
        // Fix this later, please.
        Assert.True(svg.Length > 1000, "Complex math should generate substantial SVG");
    }

    [Fact]
    public void RenderPageToSvg_WithTable_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Table Test\n\n"
                + "#table(\n"
                + "  columns: 4,\n"
                + "  [A], [B], [C], [D],\n"
                + "  [1], [2], [3], [4],\n"
                + "  [5], [6], [7], [8],\n"
                + ")"
        );

        // Act
        var svg = result.Document!.RenderPageToSvg(0);

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void RenderPageToSvg_WithCodeBlock_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Code Example\n\n"
                + "```rust\n"
                + "fn main() {\n"
                + "    println!(\"Hello, world!\");\n"
                + "    let x = 42;\n"
                + "}\n"
                + "```"
        );

        // Act
        var svg = result.Document!.RenderPageToSvg(0);

        // Assert
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void RenderAllPagesToSvg_EmptyDocument_ReturnsOneBlankPage()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("");

        // Act
        var allPages = result.Document!.RenderAllPagesToSvg();

        // Assert
        Assert.Single(allPages);
        Assert.NotEmpty(allPages[0]);
    }

    [Fact]
    public void RenderPageToSvg_PageBoundary_Works()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= P1\n#pagebreak()\n= P2\n#pagebreak()\n= P3");

        // Act - render first and last pages
        var firstPage = result.Document!.RenderPageToSvg(0);
        var lastPage = result.Document!.RenderPageToSvg(result.Document.PageCount - 1);

        // Assert
        Assert.NotEmpty(firstPage);
        Assert.NotEmpty(lastPage);
        Assert.NotEqual(firstPage, lastPage); // Should be different content
    }

    [Fact]
    public void RenderPageToSvg_AfterResultDispose_Throws()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        var result = compiler.Compile("= Test");
        var document = result.Document!;
        result.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.RenderPageToSvg(0));
    }

    [Fact]
    public void RenderPageToSvg_100Pages_AllSucceed()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        var content = string.Join(
            "\n#pagebreak()\n",
            Enumerable.Range(1, 100).Select(i => $"= Page {i}")
        );
        using var result = compiler.Compile(content);

        // Act & Assert - render all pages individually
        for (var i = 0; i < 100; i++)
        {
            var svg = result.Document!.RenderPageToSvg(i);
            Assert.NotEmpty(svg);
            Assert.Contains("<svg", svg);
        }
    }

    [Fact]
    public void Compile_LargeDocument_StressTest()
    {
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // This Typst code generates roughly 100 pages of text
        const string source =
            "#set page(height: auto)\n"
            + "= Stress Test\n"
            + "#for i in range(500) [\n  == Section #i\n  #lorem(200)\n]";

        using var result = compiler.Compile(source);

        Assert.True(result.Success);
        Assert.NotNull(result.Document);

        // 2026/01/10 - 493ms on dev machine. kinda good?
    }

    [Fact]
    public void RenderPageToSvg_WithCustomOptionsAndInputs_Succeeds()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            IncludeSystemFonts = true,
            Inputs =
            {
                ["title"] = "FFI Stress Test",
                ["author"] = "Jay",
                ["page_count"] = "3",
            },
        };

        using var compiler = new TypstCompiler(options);

        // Act - Using Typst code that relies heavily on inputs
        const string source = """
                    #let title = sys.inputs.at("title", default: "Untitled")
                    #let author = sys.inputs.at("author", default: "Unknown")
                    #let count = int(sys.inputs.at("page_count", default: "1"))

                    = #title
                    Author: #author

                    #for i in range(count) [
                        #pagebreak()
                        == Page #(i + 1)
                        This is dynamic content.
                    ]
                
            """;

        using var result = compiler.Compile(source);

        // Assert
        Assert.True(result.Success, "Compilation should succeed with valid inputs");
        Assert.NotNull(result.Document);
        Assert.Equal(4, result.Document.PageCount);

        // Verify last page renders correctly
        var lastPageSvg = result.Document.RenderPageToSvg(3);
        Assert.NotEmpty(lastPageSvg);
    }
}
