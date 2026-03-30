using Ndggr.Content;
using Ndggr.Content.Models;

namespace Ndggr.Extensions;

/// <summary>
/// High-level facade for ndggr: search DuckDuckGo and fetch URL content in one-liners.
/// </summary>
public sealed class NdggrClient : IDisposable
{
    private readonly DdgClient _searchClient;
    private readonly ContentPipeline _contentPipeline;

    public NdggrClient()
    {
        _searchClient = new DdgClient();
        _contentPipeline = new ContentPipeline();
    }

    public NdggrClient(DdgSearchOptions searchOptions)
    {
        _searchClient = new DdgClient(searchOptions);
        _contentPipeline = new ContentPipeline();
    }

    public NdggrClient(DdgSearchOptions searchOptions, ContentExtractionOptions contentOptions)
    {
        _searchClient = new DdgClient(searchOptions);
        _contentPipeline = new ContentPipeline(contentOptions);
    }

    /// <summary>
    /// Search DuckDuckGo with a simple query string.
    /// </summary>
    public Task<DdgSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
        => _searchClient.SearchAsync(query, cancellationToken: cancellationToken);

    /// <summary>
    /// Search DuckDuckGo with full options.
    /// </summary>
    public Task<DdgSearchResponse> SearchAsync(string query, DdgSearchOptions options, CancellationToken cancellationToken = default)
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
