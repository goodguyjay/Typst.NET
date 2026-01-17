namespace Typst.NET.Interop;

/// <summary>
/// Helper methods for marshaling between managed and native types.
/// </summary>
internal static class InteropHelpers
{
    /// <summary>
    /// Convert a string to a UTF-8 byte array.
    /// </summary>
    public static byte[] StringToUtf8Bytes(string str)
    {
        return System.Text.Encoding.UTF8.GetBytes(str);
    }

    /// <summary>
    /// Read UTF-8 string from native buffer.
    /// </summary>
    public static unsafe string Utf8BufferToString(byte* data, nuint length)
    {
        if (data == null || length == 0)
            return string.Empty;

        var span = new ReadOnlySpan<byte>(data, (int)length);
        return System.Text.Encoding.UTF8.GetString(span);
    }

    /// <summary>
    /// Copy native buffer to managed byte array.
    /// </summary>
    public static unsafe byte[] BufferToByteArray(Buffer buffer)
    {
        if (buffer.Data == null || buffer.Length == 0)
            return [];

        var result = new byte[buffer.Length];
        var span = new Span<byte>(buffer.Data, (int)buffer.Length);
        span.CopyTo(result);

        return result;
    }

    /// <summary>
    /// Convert native diagnostic to managed type.
    /// </summary>
    public static unsafe Diagnostics.Diagnostic ConvertDiagnostic(Diagnostic nativeDiag)
    {
        var message = Utf8BufferToString(nativeDiag.Message, nativeDiag.MessageLength);

        Diagnostics.SourceLocation? location = null;
        if (nativeDiag.Location.Line > 0)
        {
            location = new Diagnostics.SourceLocation
            {
                Line = nativeDiag.Location.Line,
                Column = nativeDiag.Location.Column,
                Length = nativeDiag.Location.Length,
            };
        }

        return new Diagnostics.Diagnostic
        {
            Severity = (Diagnostics.DiagnosticSeverity)nativeDiag.Severity,
            Message = message,
            Location = location,
        };
    }
}
