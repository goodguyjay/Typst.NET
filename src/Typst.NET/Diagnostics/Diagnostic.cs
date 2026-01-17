namespace Typst.NET.Diagnostics;

/// <summary>
/// Diagnostic message from compilation (error, warning or hint).
/// </summary>
public sealed record Diagnostic
{
    /// <summary>
    /// Severity level of this diagnostic.
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Source location (null if unavailable).
    /// </summary>
    public SourceLocation? Location { get; init; }

    /// <summary>
    /// Whether this is an error (blocks compilation).
    /// </summary>
    public bool IsError => Severity == DiagnosticSeverity.Error;

    /// <summary>
    /// Whether this is a warning (does not block compilation).
    /// </summary>
    public bool IsWarning => Severity == DiagnosticSeverity.Warning;
}
