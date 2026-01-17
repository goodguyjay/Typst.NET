using Xunit;

namespace Typst.NET.Tests;

public sealed class PdfInputsIntegrationTests
{
    [Fact]
    public void RenderToPdf_WithInputs_ProducesDifferentPdfs()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        var options1 = new TypstCompilerOptions
        {
            WorkspaceRoot = tempDir,
            Inputs = { ["title"] = "Document A" },
        };

        var options2 = new TypstCompilerOptions
        {
            WorkspaceRoot = tempDir,
            Inputs = { ["title"] = "Document B" },
        };

        const string source = "= #sys.inputs.title";

        // Act
        using var compiler1 = new TypstCompiler(options1);
        using var result1 = compiler1.Compile(source);
        var pdf1 = result1.Document!.RenderToPdf();

        using var compiler2 = new TypstCompiler(options2);
        using var result2 = compiler2.Compile(source);
        var pdf2 = result2.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf1);
        Assert.NotEmpty(pdf2);
        Assert.NotEqual(pdf1, pdf2);
    }

    [Fact]
    public void RenderToPdf_InputControlsPageCount_CorrectPageCount()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["page_count"] = "10" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            #let count = int(sys.inputs.page_count)
            #for i in range(count) [
                = Page #(i + 1)
                #if i < count - 1 [
                    #pagebreak()
                ]
            ]
            """;

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.NotEmpty(pdf);
        Assert.Equal(10, result.Document.PageCount);

        // PDF should be substantial (10 pages) I LOVE HEURISTICS (ノಥ,_｣ಥ)
        Assert.True(pdf.Length > 10_000);
    }

    [Fact]
    public void RenderToPdf_WithComplexInputDrivenContent_Succeeds()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs =
            {
                ["report_title"] = "Q4 2028 Performance",
                ["author"] = "Something Something Corporate",
                ["date"] = "2026-04-02",
                ["sections"] = "5",
            },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            = #sys.inputs.report_title
            _Prepared by: #sys.inputs.author _
            _Date: #sys.inputs.date _

            #let sections = int(sys.inputs.sections)
            #for i in range(sections) [
                #pagebreak()
                == Section #(i + 1)
                
                #lorem(100)
                
                #table(
                    columns: 3,
                    [Metric], [Q3], [Q4],
                    [Revenue], [100M], [120M],
                    [Growth], [10%], [20%]
                )
            ]
            """;

        // Act
        using var result = compiler.Compile(source);

        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));

        // Should have 1 title page + 5 sections
        Assert.Equal(6, result.Document.PageCount);
    }

    [Fact]
    public void RenderToPdf_InputsWithMath_CorrectlyRendered()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs =
            {
                ["equation"] = "quadratic",
                ["coefficient_a"] = "1",
                ["coefficient_b"] = "2",
                ["coefficient_c"] = "1",
            },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            = #sys.inputs.equation Formula

            #let a = int(sys.inputs.coefficient_a)
            #let b = int(sys.inputs.coefficient_b)
            #let c = int(sys.inputs.coefficient_c)

            $ #a x^2 + #b x + #c = 0 $
            """;

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_ConditionalContentViaInputs_OnlyIncludesRequested()
    {
        // Arrange
        var optionsWithSecret = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["include_confidential"] = "true" },
        };

        var optionsWithoutSecret = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["include_confidential"] = "false" },
        };

        const string source = """
            = Public Report
            This is public information.

            #if sys.inputs.at("include_confidential", default: "false") == "true" [
                #pagebreak()
                = Confidential Section
                This is secret information.
                #pagebreak()
                = More Secrets
                Additional confidential data.
            ]
            """;

        // Act
        using var compiler1 = new TypstCompiler(optionsWithSecret);
        using var result1 = compiler1.Compile(source);
        var pdfWithSecret = result1.Document!.RenderToPdf();

        using var compiler2 = new TypstCompiler(optionsWithoutSecret);
        using var result2 = compiler2.Compile(source);
        var pdfWithoutSecret = result2.Document!.RenderToPdf();

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);

        // With secret: 1 public + 2 secret pages
        Assert.Equal(3, result1.Document!.PageCount);

        // Without secret: only 1 public page
        Assert.Equal(1, result2.Document!.PageCount);

        // PDFs should be different sizes
        Assert.True(pdfWithSecret.Length > pdfWithoutSecret.Length);
    }

    [Fact]
    public void RenderToPdf_InputsWithUnicode_Succeeds()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs =
            {
                ["title"] = "世界报告 2024",
                ["subtitle"] = "グローバル分析",
                ["emoji"] = "🌍📊💼",
            },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            = #sys.inputs.title
            == #sys.inputs.subtitle

            Status: #sys.inputs.emoji
            """;

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void RenderToPdf_SameInputsDifferentCompilers_IdenticalOutput()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = tempDir,
            Inputs = { ["value"] = "deterministic" },
        };

        const string source = "= #sys.inputs.value";

        // Act
        using var compiler1 = new TypstCompiler(options);
        using var result1 = compiler1.Compile(source);
        var pdf1 = result1.Document!.RenderToPdf();

        using var compiler2 = new TypstCompiler(options);
        using var result2 = compiler2.Compile(source);
        var pdf2 = result2.Document!.RenderToPdf();

        // Assert
        Assert.Equal(pdf1, pdf2);
    }

    [Fact]
    public void RenderToPdf_InputsInTableGeneration_Succeeds()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["rows"] = "5", ["product"] = "Widget" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            = Sales Report: #sys.inputs.product

            #let rows = int(sys.inputs.rows)

            #table(
                columns: 3,
                [Month], [Units], [Revenue],
                ..for i in range(rows) {
                    ([Q#(i+1)], [#(i+1)00], [\$#(i+1)000])
                }
            )
            """;

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void RenderToPdf_MultipleCompilationsWithInputs_NoMemoryLeak()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["iteration"] = "0" },
        };

        using var compiler = new TypstCompiler(options);
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        // Warmup
        for (var i = 0; i < 10; i++)
        {
            using var warmup = compiler.Compile("= Warmup");
            _ = warmup.Document!.RenderToPdf();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var initialMemory = process.PrivateMemorySize64;

        // Act - 100 compilations with different input values
        for (var i = 0; i < 100; i++)
        {
            // Note: can't change inputs, but we can compile different source
            using var result = compiler.Compile($"= Iteration {i}\n\n#sys.inputs.iteration");
            var pdf = result.Document!.RenderToPdf();
            Assert.NotEmpty(pdf);
        }

        // Assert
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = process.PrivateMemorySize64;
        var memoryGrowth = finalMemory - initialMemory;

        // Should not leak significantly (< 50MB for 100 PDF renders)
        Assert.True(
            memoryGrowth < 50 * 1024 * 1024,
            $"Memory leaked: {memoryGrowth / 1024 / 1024}MB"
        );
    }

    [Fact]
    public void RenderToPdf_LargeInputValue_Succeeds()
    {
        // Arrange
        var largeContent = string.Concat(Enumerable.Repeat("Content section. ", 5000));
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["large_content"] = largeContent },
        };

        using var compiler = new TypstCompiler(options);
        const string source = "= Large Content Test\n\n#sys.inputs.large_content";

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);

        // Should produce a substantial PDF (multiple pages)
        Assert.True(pdf.Length > 50_000);
    }

    [Fact]
    public void RenderToPdf_InputsAndSvg_BothRenderCorrectly()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            Inputs = { ["title"] = "Multi-Format Test" },
        };

        using var compiler = new TypstCompiler(options);
        const string source = "= #sys.inputs.title\n\nContent here.";

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();
        var svg = result.Document!.RenderPageToSvg(0);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);
        Assert.NotEmpty(svg);

        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void RenderToPdf_EmptyInputsStillWorks()
    {
        // Arrange
        var options = new TypstCompilerOptions
        {
            WorkspaceRoot = Path.GetTempPath(),
            // ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
            Inputs = { },
        };

        using var compiler = new TypstCompiler(options);
        const string source = """
            = Document Without Inputs

            #let title = sys.inputs.at("missing", default: "Default Title")

            Using default: #title
            """;

        // Act
        using var result = compiler.Compile(source);
        var pdf = result.Document!.RenderToPdf();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(pdf);
    }
}
