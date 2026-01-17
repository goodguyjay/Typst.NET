namespace Typst.NET;

/// <summary>
/// Exception thrown when Typst compilation or rendering fails.
/// </summary>
public class TypstException : Exception
{
    public TypstException(string message)
        : base(message) { }

    public TypstException(string message, Exception innerException)
        : base(message, innerException) { }
}
