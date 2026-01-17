using System.Collections.Immutable;
using Typst.NET.Diagnostics;

namespace Typst.NET;

/// <summary>
/// Exception thrown when Typst compilation fails with diagnostics.
/// </summary>
public class TypstCompilationException(ImmutableArray<Diagnostic> diagnostics)
    : TypstException(
        $"Typst compilation failed with {diagnostics.Length} errors. First error: {diagnostics.FirstOrDefault(d => d.IsError)?.Message}"
    )
{
    public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
}
