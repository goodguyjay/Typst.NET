namespace Typst.NET.Diagnostics;

/// <summary>
/// Location in source code (1-indexed line and column).
/// </summary>
public readonly record struct SourceLocation
{
    /// <summary>
    /// Line number (1-indexed, 0 if unavailable).
    /// </summary>
    public required uint Line { get; init; }

    /// <summary>
    /// Column number (1-indexed, 0 if unavailable).
    /// </summary>
    public required uint Column { get; init; }

    /// <summary>
    /// Length of the span in characters.
    /// </summary>
    public required uint Length { get; init; }

    /// <summary>
    /// Whether the location is valid (line and column > 0).
    /// </summary>
    public bool IsValid => Line > 0 && Column > 0;
}
