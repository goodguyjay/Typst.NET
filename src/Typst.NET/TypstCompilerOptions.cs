namespace Typst.NET;

/// <summary>
/// Configuration options for the Typst compiler.
/// </summary>
public sealed class TypstCompilerOptions
{
    /// <summary>
    /// Workspace root directory path (required)
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Include system fonts (default: true)
    /// </summary>
    public bool IncludeSystemFonts { get; init; } = true;

    /// <summary>
    /// Custom input variables accessible via sys.inputs in Typst.
    /// Example: { ["author"] = "Jay", ["date"] = "2026-04-02" }
    /// </summary>
    public Dictionary<string, string> Inputs { get; init; } = [];

    // Future options (not yet implemented)

    /// <summary>
    /// Custom font directory paths.
    /// </summary>
    public List<string> CustomFontPaths { get; init; } = [];

    /// <summary>
    /// Package directory path for offline Typst packages.
    /// </summary>
    public string? PackagePath { get; init; }
}
