using Typst.NET.Diagnostics;
using Xunit;

namespace Typst.NET.Tests;

public sealed class DiagnosticTests
{
    [Fact]
    public void Compile_WithError_ErrorPropertiesPopulated()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("#let x = (unclosed");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);

        var error = result.Errors[0];
        Assert.NotNull(error.Message);
        Assert.NotEmpty(error.Message);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.True(error.IsError);
    }

    [Fact]
    public void Compile_MultipleErrors_AllCaptured()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Multiple syntax errors
        const string sourceWithErrors = "#let x = (unclosed\n" + "#let y = [unclosed\n" + "= Title";

        // Act
        using var result = compiler.Compile(sourceWithErrors);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.Errors.Length >= 2, $"Expected >= 2 errors, got {result.Errors.Length}");
    }

    [Fact]
    public void Compile_Success_ErrorsEmpty()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("= Success");

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Compile_DiagnosticMessage_NotEmpty()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        using var compiler = new TypstCompiler(tempDir);

        // Act
        using var result = compiler.Compile("#let x = (");

        // Assert
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(
            result.Diagnostics,
            diag =>
            {
                Assert.NotNull(diag.Message);
                Assert.NotEmpty(diag.Message);
            }
        );
    }

    [Fact]
    public void Diagnostic_Equality_Works()
    {
        // Arrange
        var diag1 = new Diagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = "Test error",
            Location = null,
        };

        var diag2 = new Diagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = "Test error",
            Location = null,
        };

        var diag3 = new Diagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Message = "Test warning",
            Location = null,
        };

        // Assert
        Assert.Equal(diag1, diag2);
        Assert.NotEqual(diag1, diag3);
    }

    [Fact]
    public void SourceLocation_IsValid_WorksCorrectly()
    {
        // Arrange
        var validLoc = new SourceLocation
        {
            Line = 1,
            Column = 5,
            Length = 10,
        };
        var invalidLoc = new SourceLocation
        {
            Line = 0,
            Column = 0,
            Length = 0,
        };

        // Assert
        Assert.True(validLoc.IsValid);
        Assert.False(invalidLoc.IsValid);
    }
}
