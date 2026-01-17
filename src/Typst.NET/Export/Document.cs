using Typst.NET.Interop;
using Buffer = Typst.NET.Interop.Buffer;

namespace Typst.NET.Export;

/// <summary>
/// Compiled typst document that can be rendered to various formats.
/// Must be disposed to free native resources.
/// </summary>
public sealed class Document : IDisposable
{
    private nint Handle { get; }

    /// <summary>
    /// Number of pages in the document
    /// </summary>
    public int PageCount { get; init; }

    private bool _disposed;

    internal Document(nint handle, int pageCount)
    {
        if (handle == 0)
            throw new ArgumentException("Invalid document handle", nameof(handle));

        Handle = handle;
        PageCount = pageCount;
    }

    internal void MarkDisposed()
    {
        _disposed = true;
    }

    /// <summary>
    /// Render a single page to SVG.
    /// </summary>
    /// <param name="pageIndex">Zero-indexed page number.</param>
    /// <returns>SVG markup as UTF-8 string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Page index out of bounds.</exception>
    /// <exception cref="TypstException">Rendering failed.</exception>
    public string RenderPageToSvg(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (pageIndex < 0 || pageIndex >= PageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                $"Page index {pageIndex} is out of range (0-{PageCount - 1})"
            );
        }

        unsafe
        {
            var buffer = NativeMethods.typst_net_document_render_svg_page(Handle, (nuint)pageIndex);

            if (buffer.Data == null || buffer.Length == 0)
                throw new TypstException($"Failed to render page {pageIndex} to SVG");

            try
            {
                var span = new ReadOnlySpan<byte>(buffer.Data, (int)buffer.Length);
                return System.Text.Encoding.UTF8.GetString(span);
            }
            finally
            {
                NativeMethods.typst_net_buffer_free(buffer);
            }
        }
    }

    /// <summary>
    /// Render all pages to SVG.
    /// </summary>
    /// <returns>Array of SVG markup strings (one per page).</returns>
    /// <exception cref="TypstException">Rendering failed.</exception>
    public string[] RenderAllPagesToSvg()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            var array = NativeMethods.typst_net_document_render_svg_all(Handle);

            if (array.Buffers == null || array.Length == 0)
                throw new TypstException("Failed to render pages to SVG");

            try
            {
                var results = new string[array.Length];
                var buffers = new Span<Buffer>(array.Buffers, (int)array.Length);

                for (var i = 0; i < buffers.Length; i++)
                {
                    results[i] = InteropHelpers.Utf8BufferToString(
                        buffers[i].Data,
                        buffers[i].Length
                    );
                }

                return results;
            }
            finally
            {
                NativeMethods.typst_net_buffer_array_free(array);
            }
        }
    }

    /// <summary>
    /// Render document to PDF.
    /// </summary>
    /// <returns>PDF file as byte array.</returns>
    /// <exception cref="TypstException">Rendering failed.</exception>
    /// <exception cref="ObjectDisposedException">Document has been disposed.</exception>
    public byte[] RenderToPdf()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            var buffer = NativeMethods.typst_net_document_render_pdf(Handle);

            if (buffer.Data == null || buffer.Length == 0)
                throw new TypstException("Failed to render pdf");

            try
            {
                // Allocate managed array and copy from native memory
                var result = new byte[(int)buffer.Length];
                var source = new ReadOnlySpan<byte>(buffer.Data, (int)buffer.Length);
                source.CopyTo(result);
                return result;
            }
            finally
            {
                NativeMethods.typst_net_buffer_free(buffer);
            }
        }
    }

    public void Dispose()
    {
        MarkDisposed();
        GC.SuppressFinalize(this);
    }

    ~Document() => Dispose();
}
