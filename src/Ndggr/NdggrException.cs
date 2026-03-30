namespace Ndggr;

/// <summary>
/// Base exception for all ndggr-specific errors.
/// </summary>
public class NdggrException : Exception
{
    public NdggrException(string message) : base(message) { }
    public NdggrException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a search request fails due to network or HTTP errors.
/// </summary>
public class SearchException : NdggrException
{
    public int? StatusCode { get; }

    public SearchException(string message) : base(message) { }
    public SearchException(string message, int statusCode) : base(message) { StatusCode = statusCode; }
    public SearchException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when DuckDuckGo returns a rate limit response (HTTP 429 or CAPTCHA).
/// </summary>
public class RateLimitException : SearchException
{
    public RateLimitException()
        : base("Rate limited by DuckDuckGo. Please wait and try again.") { }

    public RateLimitException(string message) : base(message) { }
}
