using WebExplorer.Content;
using WebExplorer.Content.Models;

namespace WebExplorer.Extensions;

/// <summary>
/// High-level facade for web-explorer: search DuckDuckGo and fetch URL content in one-liners.
/// </summary>
public sealed class WebExplorerClient : IDisposable
{
    private readonly SearchClient _searchClient;
    private readonly ContentPipeline _contentPipeline;

    public WebExplorerClient()
    {
        _searchClient = new SearchClient();
        _contentPipeline = new ContentPipeline();
    }

    public WebExplorerClient(SearchOptions searchOptions)
    {
        _searchClient = new SearchClient(searchOptions);
        _contentPipeline = new ContentPipeline();
    }

    public WebExplorerClient(SearchOptions searchOptions, ContentExtractionOptions contentOptions)
    {
        _searchClient = new SearchClient(searchOptions);
        _contentPipeline = new ContentPipeline(contentOptions);
    }

    /// <summary>
    /// Search DuckDuckGo with a simple query string.
    /// </summary>
    public Task<SearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
        => _searchClient.SearchAsync(query, cancellationToken: cancellationToken);

    /// <summary>
    /// Search DuckDuckGo with full options.
    /// </summary>
    public Task<SearchResponse> SearchAsync(string query, SearchOptions options, CancellationToken cancellationToken = default)
        => _searchClient.SearchAsync(query, options, cancellationToken);

    /// <summary>
    /// Fetch a URL and return the content as Markdown.
    /// </summary>
    public async Task<string> FetchMarkdownAsync(string url, CancellationToken cancellationToken = default)
    {
        var doc = await _contentPipeline.ProcessAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
        return doc.Markdown ?? "";
    }

    /// <summary>
    /// Fetch a URL and return a structured ContentDocument (JSON-serializable).
    /// </summary>
    public Task<ContentDocument> FetchAsync(string url, ContentExtractionOptions? options = null, CancellationToken cancellationToken = default)
        => _contentPipeline.ProcessAsync(url, options, cancellationToken);

    public void Dispose()
    {
        _searchClient.Dispose();
        _contentPipeline.Dispose();
    }
}
