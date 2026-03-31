namespace WebExplorer;

/// <summary>
/// Options for a DuckDuckGo search request.
/// </summary>
public sealed record SearchOptions
{
    /// <summary>
    /// Number of results per page (0–25, default 10). 0 means use DDG default.
    /// </summary>
    public int NumResults { get; init; } = 10;

    /// <summary>
    /// Region code, e.g. "us-en", "de-de".
    /// </summary>
    public string Region { get; init; } = "us-en";

    /// <summary>
    /// Time filter: "d" (day), "w" (week), "m" (month), "y" (year), or empty.
    /// </summary>
    public string TimeFilter { get; init; } = "";

    /// <summary>
    /// Restrict search to a specific site, e.g. "github.com".
    /// </summary>
    public string? Site { get; init; }

    /// <summary>
    /// Enable safe search (default true).
    /// </summary>
    public bool SafeSearch { get; init; } = true;

    /// <summary>
    /// Send a User-Agent header. No longer used — Primp handles browser headers automatically.
    /// </summary>
    [Obsolete("PrimpClient manages browser headers automatically. This property is ignored.")]
    public bool SendUserAgent { get; init; } = true;

    /// <summary>
    /// HTTPS proxy URI, e.g. "http://proxy:8080".
    /// </summary>
    public Uri? Proxy { get; init; }
}
