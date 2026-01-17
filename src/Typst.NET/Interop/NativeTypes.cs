using System.Runtime.InteropServices;

namespace Typst.NET.Interop;

/// <summary>
/// Buffer containing raw bytes allocated by Rust.
/// Must be freed with typst_buffer_free.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Buffer
{
    public byte* Data;
    public nuint Length;
}

/// <summary>
/// Array of buffers allocated by Rust.
/// Must be freed with typst_buffer_array_free.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct BufferArray
{
    public Buffer* Buffers;
    public nuint Length;
}

/// <summary>
/// Diagnostic severity levels matching Rust enum.
/// repr(u8) in Rust corresponds to byte in C#.
/// </summary>
internal enum DiagnosticSeverity : byte
{
    Error = 0,
    Warning = 1,
    Hint = 2,
}

/// <summary>
/// Source location for diagnostics (1-indexed).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SourceLocation
{
    public uint Line;
    public uint Column;
    public uint Length;
}

/// <summary>
/// Diagnostic message from compilation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Diagnostic
{
    public DiagnosticSeverity Severity;
    public byte* Message;
    public nuint MessageLength;
    public SourceLocation Location;
}

/// <summary>
/// Result of compilation containing document and diagnostic.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct CompileResult
{
    public bool Success;
    public Diagnostic* Diagnostics;
    public nuint DiagnosticsLength;
    public void* Document;
}

/// <summary>
/// Native compiler configurable options.
/// All pointers are borrowed. Caller retains ownership.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CompilerOptions
{
    /// <summary>
    /// Include system fonts (default: true)
    /// </summary>
    [MarshalAs(UnmanagedType.U1)]
    public bool IncludeSystemFonts;

    /// <summary>
    /// JSON string of inputs: { "key": "value", ... } (borrowed pointer)
    /// </summary>
    public unsafe byte* InputsJson;
    public nuint InputsJsonLength;

    /// <summary>
    /// Custom font paths array
    /// </summary>
    public unsafe byte* CustomFontPaths;
    public nuint CustomFontPathsLength;

    /// <summary>
    /// Package path for offline packages
    /// </summary>
    public unsafe byte* PackagePath;
    public nuint PackagePathLength;
}
