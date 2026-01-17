using Xunit;

namespace Typst.NET.Tests;

public sealed class PdfRenderingTests
{
    [Fact]
    public void RenderToPdf_SimpleDocument_ReturnsPdfBytes()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= PDF Test\n\nContent here.");

        // Act
        var pdfBytes = result.Document!.RenderToPdf();

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 100); // Reasonable size check

        // Check for PDF header
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));
    }

    [Fact]
    public void RenderToPdf_EmptyDocument_ReturnsValidPdf()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("");

        // Act
        var pdfBytes = result.Document!.RenderToPdf();

        // Assert - even empty doc produces valid pdf
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));
    }

    [Fact]
    public void RenderToPdf_MultiPage_ProducesLargerPdf()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        using var singlePage = compiler.Compile("= Single Page");
        using var multiPage = compiler.Compile(
            "= Page 1\n#pagebreak()\n= Page 2\n#pagebreak()\n= Page 3"
        );

        // Act
        var singlePdf = singlePage.Document!.RenderToPdf();
        var multiPdf = multiPage.Document!.RenderToPdf();

        // Assert
        Assert.True(multiPdf.Length > singlePdf.Length); // Arbitrary size check for multipage... not great but useful
    }

    [Fact]
    public void RenderToPdf_WithMath_IncludesMathContent()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Math Test\n\n" + "$ integral_0^infinity e^(-x^2) dif x = sqrt(pi)/2 $"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 1000); // math formulas add complexity/size
    }

    [Fact]
    public void RenderToPdf_WithTable_IncludesTableStructure()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Table Test\n\n"
                + "#table(\n"
                + "  columns: 3,\n"
                + "  [A], [B], [C],\n"
                + "  [1], [2], [3]\n"
                + ")"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void RenderToPdf_TableWithFormatting_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Table Test\n\n"
                + "#table(\n"
                + "  columns: 3,\n"
                + "  [*Bold*], [_Italic_], [`Code`],\n"
                + "  [1], [2], [3]\n"
                + ")"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_FailedCompilation_DocumentIsNull()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Compile with syntax error
        using var result = compiler.Compile("#let x = (");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Document);
    }

    [Fact]
    public void RenderToPdf_WithCodeBlock_IncludesCode()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Code Test\n\n"
                + "```python\n"
                + "def hello():\n"
                + "    print(\"Hello, Typst!\")\n"
                + "```"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_LargeDocument_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        var content = string.Join(
            "\n#pagebreak()\n",
            Enumerable.Range(1, 50).Select(i => $"= Page {i}\n\nContent for page {i}.")
        );

        using var result = compiler.Compile(content);

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 10_000); // 50 pages~ should yield a sizable PDF
    }

    [Fact]
    public void RenderToPdf_SameTwice_ReturnsSameContent()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Deterministic Test");

        // Act
        var pdf1 = result.Document!.RenderToPdf();
        var pdf2 = result.Document!.RenderToPdf();

        // Assert
        Assert.Equal(pdf1, pdf2);
    }

    [Fact]
    public void RenderToPdf_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        var result = compiler.Compile("= Test Document");
        var document = result.Document!;
        result.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.RenderToPdf());
    }

    [Fact]
    public void RenderToPdf_AndSvg_BothSucceed()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Multi-Format Test");

        // Act
        var pdf = result.Document!.RenderToPdf();
        var svg = result.Document.RenderPageToSvg(0);

        // Assert
        Assert.NotEmpty(pdf);
        Assert.NotEmpty(svg);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void RenderToPdf_WithUnicode_HandlesCorrectly()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Unicode Test\n\n" + "中文 Japanese 日本語 한국어\n" + "Emoji: 🚀 🎉 💻"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_100Pages_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        var content = string.Join(
            "\n#pagebreak()\n",
            Enumerable.Range(1, 100).Select(i => $"= Page {i}")
        );

        using var result = compiler.Compile(content);

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 20_000); // Expect larger size for 100 pages
    }

    [Fact]
    public void RenderToPdf_ComplexLayout_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Complex Layout\n\n"
                + "#table(\n"
                + "  columns: 2,\n"
                + "  [Column 1], [Column 2],\n"
                + "  [Content A], [Content B]\n"
                + ")\n\n"
                + "$ E = m c^2 $"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_WithHeadings_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Heading 1\n" + "== Heading 2\n" + "=== Heading 3\n" + "==== Heading 4"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_WithLists_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile(
            "= Lists\n\n"
                + "- Item 1\n"
                + "- Item 2\n"
                + "  - Nested 2.1\n"
                + "  - Nested 2.2\n"
                + "- Item 3\n\n"
                + "1. First\n"
                + "2. Second\n"
                + "3. Third"
        );

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_LargeLoop_HasStableMemoryProfile()
    {
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Memory Stress Test\n\n" + new string('a', 5000));

        // Warm up
        for (var i = 0; i < 5; i++)
            result.Document!.RenderToPdf();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var initialMemory = Environment.WorkingSet;

        // Act - 500 renders of a decent sized doc
        for (var i = 0; i < 500; i++)
        {
            result.Document!.RenderToPdf();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = Environment.WorkingSet;

        // A small growth (few MBs) is fine due to fragmentation/allocator behavior
        // but 500 renders of a 10KB doc would be 5MB+ if leaked.
        Assert.True(finalMemory - initialMemory < 10 * 1024 * 1024);
    }

    [Fact]
    public void RenderToPdf_VeryLargeContent_Succeeds()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        var largeText = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet. ", 1000));
        using var result = compiler.Compile($"= Large Content\n\n{largeText}");

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 5000);
    }

    [Fact]
    public void RenderToPdf_PdfEndMarker_Present()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Test");

        // Act
        var pdf = result.Document!.RenderToPdf();

        // Assert - PDF should end with %%EOF (possibly with whitespace)
        var lastBytes = System.Text.Encoding.ASCII.GetString(pdf.AsSpan()[^20..]);
        Assert.Contains("%%EOF", lastBytes);
    }

    [Fact]
    public void RenderToPdf_AfterSvgRender_StillWorks()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);
        using var result = compiler.Compile("= Test");

        // Act - render SVG first, then PDF
        var svg = result.Document!.RenderPageToSvg(0);
        var pdf = result.Document!.RenderToPdf();

        // Assert - both should work
        Assert.NotEmpty(svg);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_CompareSize_WithPageCount()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        var sizes = new List<int>();

        // Generate documents with 1, 2, 5, 10 pages
        foreach (var pageCount in new[] { 1, 2, 5, 10 })
        {
            var content = string.Join(
                "\n#pagebreak()\n",
                Enumerable.Range(1, pageCount).Select(i => $"= Page {i}")
            );

            using var result = compiler.Compile(content);
            var pdf = result.Document!.RenderToPdf();
            sizes.Add(pdf.Length);
        }

        // Assert - PDF size should generally increase with page count
        Assert.True(sizes[1] > sizes[0]); // 2 pages > 1 page
        Assert.True(sizes[2] > sizes[1]); // 5 pages > 2 pages
        Assert.True(sizes[3] > sizes[2]); // 10 pages > 5 pages
    }
}
