using System.Collections.Immutable;
using System.Text.Json;
using Typst.NET.Export;
using Typst.NET.Interop;
using Diagnostic = Typst.NET.Diagnostics.Diagnostic;

namespace Typst.NET;

/// <summary>
/// Stateful Typst compiler instance.
/// Maintains workspace context and font cache across compilations.
/// </summary>
public sealed class TypstCompiler : IDisposable
{
    private readonly SafeCompilerHandle? _handle;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypstCompiler"/> class using the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration settings for the compiler, including workspace root, inputs, and font settings.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or its <see cref="TypstCompilerOptions.WorkspaceRoot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the <see cref="TypstCompilerOptions.WorkspaceRoot"/> directory does not exist or is invalid.</exception>
    /// <exception cref="TypstException">Thrown if the native Typst engine fails to initialize with the provided configuration.</exception>
    public TypstCompiler(TypstCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.WorkspaceRoot);

        if (!Directory.Exists(options.WorkspaceRoot))
            throw new ArgumentException(
                $"Workspace root does not exist: {options.WorkspaceRoot}",
                nameof(options)
            );

        var rootBytes = InteropHelpers.StringToUtf8Bytes(options.WorkspaceRoot);

        var inputBytes =
            options.Inputs.Count > 0 ? JsonSerializer.SerializeToUtf8Bytes(options.Inputs) : [];

        var packageBytes =
            options.PackagePath != null
                ? InteropHelpers.StringToUtf8Bytes(options.PackagePath)
                : [];

        var fontPathBytes =
            options.CustomFontPaths.Count > 0
                ? JsonSerializer.SerializeToUtf8Bytes(options.CustomFontPaths)
                : [];

        nint handlerPtr = 0;
        try
        {
            unsafe
            {
                fixed (byte* rootPtr = rootBytes)
                fixed (byte* inputsPtr = inputBytes.Length > 0 ? inputBytes : null)
                fixed (byte* pkgPtr = packageBytes.Length > 0 ? packageBytes : null)
                fixed (byte* fontsPtr = packageBytes.Length > 0 ? fontPathBytes : null)
                {
                    var nativeOptions = new CompilerOptions
                    {
                        IncludeSystemFonts = options.IncludeSystemFonts,
                        InputsJson = inputsPtr,
                        InputsJsonLength = (nuint)inputBytes.Length,
                        CustomFontPaths = fontsPtr,
                        CustomFontPathsLength = (nuint)fontPathBytes.Length,
                        PackagePath = pkgPtr,
                        PackagePathLength = (nuint)packageBytes.Length,
                        // Add more options here as needed
                    };

                    handlerPtr = NativeMethods.typst_net_compiler_create(
                        rootPtr,
                        (nuint)rootBytes.Length,
                        &nativeOptions
                    );

                    if (handlerPtr == 0)
                        throw new TypstException(
                            $"Failed to create compiler for workspace: {options.WorkspaceRoot}"
                        );

                    // If it fails here, the catch block will free the native handler (unlikely, but happened somehow in the tests)
                    _handle = new SafeCompilerHandle(handlerPtr);
                }
            }
        }
        catch
        {
            if (handlerPtr != 0)
                NativeMethods.typst_net_compiler_free(handlerPtr);
            throw;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypstCompiler"/> class with default options.
    /// </summary>
    /// <param name="workspaceRoot">The path to the workspace root directory where Typst will look for files.</param>
    /// <param name="includeSystemFonts">If <see langword="true"/>, the compiler will attempt to load and index fonts installed on the host operating system.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="workspaceRoot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="workspaceRoot"/> path is invalid or the directory does not exist.</exception>
    /// <exception cref="TypstException">Thrown if the native compiler instance could not be initialized (e.g., due to memory or font loading errors).</exception>
    public TypstCompiler(string workspaceRoot, bool includeSystemFonts = true)
        : this(
            new TypstCompilerOptions
            {
                WorkspaceRoot = workspaceRoot,
                IncludeSystemFonts = includeSystemFonts,
            }
        ) { }

    /// <summary>
    /// Compile typst source code into a document result.
    /// </summary>
    /// <param name="source">Typst markup source code.</param>
    /// <returns>A <see cref="CompileResult"/> containing the compiled document and any diagnostics.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the compiler instance has been disposed.</exception>
    public CompileResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var byteCount = System.Text.Encoding.UTF8.GetByteCount(source);

        // For small strings (<= 4KB), stackalloc is used to allocate memory on the Stack.
        // For larger strings, a buffer is "rented" from the ArrayPool. This reuses existing
        // memory arrays instead of allocating new ones on the Heap.
        scoped Span<byte> buffer;
        byte[]? rentedArray = null;

        if (byteCount <= 4096)
        {
            buffer = stackalloc byte[byteCount];
        }
        else
        {
            rentedArray = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
            buffer = rentedArray.AsSpan(0, byteCount);
        }

        try
        {
            System.Text.Encoding.UTF8.GetBytes(source, buffer);

            unsafe
            {
                fixed (byte* sourcePtr = buffer)
                {
                    var result = NativeMethods.typst_net_compiler_compile(
                        _handle!.DangerousGetHandle(),
                        sourcePtr,
                        (nuint)byteCount
                    );

                    return ConvertCompileResult(result);
                }
            }
        }
        finally
        {
            if (rentedArray != null)
                System.Buffers.ArrayPool<byte>.Shared.Return(rentedArray);
        }
    }

    /// <summary>
    /// Reset the compilation cache.
    /// For long-running processes, call periodically to free old cached data.
    /// </summary>
    /// <param name="maxAge">Evict cache entries older than this (zero = evict all)</param>
    public static void ResetCache(TimeSpan maxAge = default)
    {
        var seconds = maxAge == TimeSpan.Zero ? 0UL : (ulong)maxAge.TotalSeconds;
        NativeMethods.typst_net_reset_cache(seconds);
    }

    private static unsafe CompileResult ConvertCompileResult(NET.Interop.CompileResult nativeResult)
    {
        var allBuilder = ImmutableArray.CreateBuilder<Diagnostic>(
            (int)nativeResult.DiagnosticsLength
        );
        var errBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
        var warnBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

        var diagSpan = new Span<NET.Interop.Diagnostic>(
            nativeResult.Diagnostics,
            (int)nativeResult.DiagnosticsLength
        );

        foreach (var nativeDiag in diagSpan)
        {
            var diag = InteropHelpers.ConvertDiagnostic(nativeDiag);
            allBuilder.Add(diag);

            if (diag.IsError)
                errBuilder.Add(diag);
            else if (diag.IsWarning)
                warnBuilder.Add(diag);
        }

        Document? document = null;
        if (nativeResult.Success && nativeResult.Document != null)
        {
            var docPtr = (nint)nativeResult.Document;
            var pageCount = (int)NativeMethods.typst_net_document_page_count(docPtr);
            document = new Document(docPtr, pageCount);
        }

        return new CompileResult
        {
            Success = nativeResult.Success,
            Diagnostics = allBuilder.MoveToImmutable(),
            Errors = errBuilder.ToImmutable(),
            Warnings = warnBuilder.ToImmutable(),
            Document = document,
            NativeResult = nativeResult,
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _handle?.Dispose();
        GC.SuppressFinalize(this);
    }

    ~TypstCompiler() => Dispose();
}
