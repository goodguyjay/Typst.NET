using System.Collections.Immutable;
using Typst.NET.Export;
using Typst.NET.Interop;
using Diagnostic = Typst.NET.Diagnostics.Diagnostic;

namespace Typst.NET;

/// <summary>
/// Result of a compilation containing document and diagnostics.
/// Must be disposed to free native resources.
/// </summary>
public sealed class CompileResult : IDisposable
{
    /// <summary>
    /// Whether the compilation was succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// All diagnostic messages (errors, warnings, hints).
    /// </summary>
    public required ImmutableArray<Diagnostic> Diagnostics { get; init; }

    /// <summary>
    /// All error diagnostics.
    /// </summary>
    public required ImmutableArray<Diagnostic> Errors { get; init; }

    /// <summary>
    /// All warning diagnostics.
    /// </summary>
    public required ImmutableArray<Diagnostic> Warnings { get; init; }

    /// <summary>
    /// Compiled document (null if compilation failed).
    /// </summary>
    public required Document? Document { get; init; }

    /// <summary>
    /// Throws <see cref="TypstCompilationException"/> if compilation failed.
    /// </summary>
    public CompileResult ThrowIfFailed()
    {
        return !Success
            ? throw new TypstException(
                $"Compilation failed with {Errors.Length} errors. First: {Errors.FirstOrDefault()?.Message}"
            )
            : this;
    }

    /// <summary>
    /// Native compile result handle.
    /// </summary>
    internal NET.Interop.CompileResult NativeResult { get; init; }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Mark document as disposed (it's now invalid)
        Document?.MarkDisposed();

        // Free the native result (which frees document + diagnostics)
        NativeMethods.typst_net_result_free(NativeResult);

        GC.SuppressFinalize(this);
    }

    ~CompileResult() => Dispose();
}
