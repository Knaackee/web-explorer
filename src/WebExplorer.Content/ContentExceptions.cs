namespace WebExplorer.Content;

/// <summary>
/// Thrown when content fetching fails.
/// </summary>
public class ContentFetchException : WebExplorerException
{
    public int? StatusCode { get; }

    public ContentFetchException(string message) : base(message) { }
    public ContentFetchException(string message, int statusCode) : base(message) { StatusCode = statusCode; }
    public ContentFetchException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when content extraction fails (parsing, readability, etc).
/// </summary>
public class ContentExtractionException : WebExplorerException
{
    public ContentExtractionException(string message) : base(message) { }
    public ContentExtractionException(string message, Exception innerException) : base(message, innerException) { }
}
