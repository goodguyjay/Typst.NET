namespace Typst.NET.Diagnostics;

/// <summary>
/// Severity level of a diagnostic message.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Fatal error that prevents compilation.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Warning about potential issues.
    /// </summary>
    Warning = 1,

    // Note: Hints are not a separate severity in typst.
    // They are additional information appended to Error/Warning messages.
}
