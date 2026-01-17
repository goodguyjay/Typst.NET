using System.Reflection;
using System.Runtime.InteropServices;

namespace Typst.NET.Interop;

/// <summary>
/// Raw P/Invoke declarations for typst_net_core native library.
/// All methods are source-generated via LibraryImport.
/// </summary>
internal static partial class NativeMethods
{
    private const string LibraryName = "typst_net_core";

    // Static constructor runs once when class is first accessed
    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, DllImportResolver);
    }

    private static IntPtr DllImportResolver(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    )
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        string platform;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            platform = "win";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            platform = "linux";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platform = "osx";
        else
            return IntPtr.Zero;

        // TODO: WASM support
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}"
            ),
        };

        var rid = $"{platform}-{arch}";

        var assemblyDir = Path.GetDirectoryName(assembly.Location)!;
        var nativeDir = Path.Combine(assemblyDir, "runtimes", rid, "native");

        var libraryFileName = platform switch
        {
            "win" => "typst_net_core.dll",
            "linux" => "libtypst_net_core.so",
            "osx" => "libtypst_net_core.dylib",
            _ => throw new PlatformNotSupportedException(),
        };

        var libraryPath = Path.Combine(nativeDir, libraryFileName);

        return !File.Exists(libraryPath)
            ? throw new DllNotFoundException($"Native library not found at: {libraryPath}")
            : NativeLibrary.Load(libraryPath);
    }

    #region VERSION INFORMATION
    // ========================================================================
    // VERSION INFORMATION (added just for debugging purposes, may come in handy later)
    // ========================================================================

    [LibraryImport(LibraryName)]
    internal static unsafe partial byte* typst_net_version();

    [LibraryImport(LibraryName)]
    internal static unsafe partial nuint typst_net_version_len();
    #endregion

    #region COMPILER LIFECYCLE
    // ========================================================================
    // COMPILER LIFECYCLE
    // ========================================================================

    /// <summary>
    /// Create a new compiler instance.
    /// </summary>
    /// <param name="rootPath">UTF-8 encoded workspace root path</param>
    /// <param name="rootPathLen">Length of rootPath in bytes</param>
    /// <param name="options">Pointer to compiler options</param>
    /// <returns>Compiler handle or null on failure</returns>
    [LibraryImport(LibraryName)]
    internal static unsafe partial nint typst_net_compiler_create(
        byte* rootPath,
        nuint rootPathLen,
        CompilerOptions* options
    );

    /// <summary>
    /// Free a compiler instance.
    /// </summary>
    [LibraryImport(LibraryName)]
    internal static partial void typst_net_compiler_free(nint compiler);
    #endregion

    #region COMPILATION
    // ========================================================================
    // COMPILATION
    // ========================================================================

    /// <summary>
    /// Compile typst source code.
    /// </summary>
    /// <param name="compiler">Valid compiler handle</param>
    /// <param name="source">UTF-8 encoded source code</param>
    /// <param name="sourceLen">Length of source in bytes</param>
    /// <returns>Compilation result. Must be freed with typst_net_result_free</returns>
    [LibraryImport(LibraryName)]
    internal static unsafe partial CompileResult typst_net_compiler_compile(
        nint compiler,
        byte* source,
        nuint sourceLen
    );

    /// <summary>
    /// Free a compilation result.
    /// </summary>
    [LibraryImport(LibraryName)]
    internal static partial void typst_net_result_free(CompileResult result);
    #endregion

    #region DOCUMENT OPERATIONS
    // ========================================================================
    // DOCUMENT OPERATIONS
    // ========================================================================

    /// <summary>
    /// Get the number of pages in a document.
    /// </summary>
    [LibraryImport(LibraryName)]
    internal static unsafe partial nuint typst_net_document_page_count(nint document);

    /// <summary>
    /// Render a single page to SVG.
    /// </summary>
    /// <param name="document">Valid document pointer</param>
    /// <param name="pageIndex">Zero-indexed page number</param>
    /// <returns>Buffer containing SVG. Must be freed with typst_net_buffer_free</returns>
    [LibraryImport(LibraryName)]
    internal static unsafe partial Buffer typst_net_document_render_svg_page(
        nint document,
        nuint pageIndex
    );

    /// <summary>
    /// Render all pages to SVG.
    /// </summary>
    /// <returns>Array of buffers - must be freed with typst_net_buffer_array_free</returns>
    [LibraryImport(LibraryName)]
    internal static unsafe partial BufferArray typst_net_document_render_svg_all(nint document);

    /// <summary>
    /// Render document to PDF (currently unimplemented).
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    [LibraryImport(LibraryName)]
    internal static unsafe partial Buffer typst_net_document_render_pdf(nint document);
    #endregion

    #region MEMORY MANAGEMENT
    // ========================================================================
    // MEMORY MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Free a buffer allocated by Rust.
    /// </summary>
    [LibraryImport(LibraryName)]
    internal static partial void typst_net_buffer_free(Buffer buffer);

    /// <summary>
    /// Free a buffer array allocated by Rust.
    /// </summary>
    [LibraryImport(LibraryName)]
    internal static partial void typst_net_buffer_array_free(BufferArray bufferArray);
    #endregion

    #region CACHE MANAGEMENT
    // ========================================================================
    // CACHE MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Reset the compilation cache.
    /// </summary>
    /// <param name="maxAgeSeconds">Evict entries older than this (0 = all)</param>
    [LibraryImport(LibraryName)]
    internal static partial void typst_net_reset_cache(ulong maxAgeSeconds);
    #endregion
}
