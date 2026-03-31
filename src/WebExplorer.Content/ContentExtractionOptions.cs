namespace WebExplorer.Content;

/// <summary>
/// Options for content fetching and extraction.
/// </summary>
public sealed record ContentExtractionOptions
{
    /// <summary>Extract only main content, stripping boilerplate (default: true).</summary>
    public bool MainContentOnly { get; init; } = true;

    /// <summary>Include extracted links in the output.</summary>
    public bool IncludeLinks { get; init; }

    /// <summary>Target chunk size in characters (0 = no chunking).</summary>
    public int ChunkSize { get; init; }

    /// <summary>Maximum number of chunks (0 = unlimited).</summary>
    public int MaxChunks { get; init; }

    /// <summary>HTTP request timeout in milliseconds.</summary>
    public int TimeoutMs { get; init; } = 30_000;

    /// <summary>Number of retries for transient errors.</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>HTTPS proxy URI.</summary>
    public Uri? Proxy { get; init; }

    /// <summary>Custom User-Agent string.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Additional HTTP headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Output schema version (default: 1).</summary>
    public int SchemaVersion { get; init; } = 1;
}
