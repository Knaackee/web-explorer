using WebExplorer.Content;
using WebExplorer.Content.Models;
using WebExplorer.Playwright;

namespace WebExplorer.Extensions;

/// <summary>
/// High-level facade for web-explorer: search DuckDuckGo and fetch URL content in one-liners.
/// </summary>
public sealed class WebExplorerClient : IDisposable
{
    private readonly SearchClient _searchClient;
    private readonly ContentPipeline _contentPipeline;
    private readonly IPlaywrightSessionManager _playwrightSessionManager;
    private readonly PlaywrightSharedBrowserPool _playwrightSharedBrowserPool;

    public WebExplorerClient()
    {
        _searchClient = new SearchClient();
        _contentPipeline = new ContentPipeline();
        _playwrightSessionManager = new PlaywrightSessionManager();
        _playwrightSharedBrowserPool = new PlaywrightSharedBrowserPool();
    }

    public WebExplorerClient(SearchOptions searchOptions)
    {
        _searchClient = new SearchClient(searchOptions);
        _contentPipeline = new ContentPipeline();
        _playwrightSessionManager = new PlaywrightSessionManager();
        _playwrightSharedBrowserPool = new PlaywrightSharedBrowserPool();
    }

    public WebExplorerClient(SearchOptions searchOptions, ContentExtractionOptions contentOptions)
        : this(searchOptions, contentOptions, new PlaywrightSessionManager(), new PlaywrightSharedBrowserPool())
    {
    }

    public WebExplorerClient(
        SearchOptions searchOptions,
        ContentExtractionOptions contentOptions,
        IPlaywrightSessionManager playwrightSessionManager,
        PlaywrightSharedBrowserPool? playwrightSharedBrowserPool = null)
    {
        _searchClient = new SearchClient(searchOptions);
        _contentPipeline = new ContentPipeline(contentOptions);
        _playwrightSessionManager = playwrightSessionManager;
        _playwrightSharedBrowserPool = playwrightSharedBrowserPool ?? new PlaywrightSharedBrowserPool();
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

    public Task<PlaywrightSessionInfo> StartPlaywrightSessionAsync(
        PlaywrightSessionStartOptions? options = null,
        CancellationToken cancellationToken = default)
        => _playwrightSessionManager.StartSessionAsync(options, cancellationToken);

    public Task<PlaywrightSessionInfo> ResumePlaywrightSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _playwrightSessionManager.ResumeSessionAsync(sessionId, cancellationToken);

    public Task EndPlaywrightSessionAsync(string sessionId, bool deleteSessionData = false, CancellationToken cancellationToken = default)
        => _playwrightSessionManager.EndSessionAsync(sessionId, deleteSessionData, cancellationToken);

    public Task<IReadOnlyList<PlaywrightSessionInfo>> ListPlaywrightSessionsAsync(CancellationToken cancellationToken = default)
        => _playwrightSessionManager.ListSessionsAsync(cancellationToken);

    public Task<PlaywrightSessionInfo?> GetPlaywrightSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _playwrightSessionManager.GetSessionAsync(sessionId, cancellationToken);

    public async Task<ContentDocument> FetchWithPlaywrightSessionAsync(
        string sessionId,
        string url,
        ContentExtractionOptions? options = null,
        PlaywrightNavigationOptions? navigationOptions = null,
        CancellationToken cancellationToken = default)
    {
        var html = await _playwrightSessionManager.FetchHtmlAsync(sessionId, url, navigationOptions, cancellationToken)
            .ConfigureAwait(false);
        return _contentPipeline.ExtractFromHtml(html, url, options);
    }

    public async Task<ContentDocument> FetchWithPlaywrightAsync(
        string url,
        ContentExtractionOptions? options = null,
        PlaywrightNavigationOptions? navigationOptions = null,
        PlaywrightSessionStartOptions? sessionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var html = await _playwrightSharedBrowserPool.FetchHtmlAsync(
            url,
            navigationOptions,
            new PlaywrightSharedBrowserOptions
            {
                Headless = sessionOptions?.Headless ?? true,
                Proxy = sessionOptions?.Proxy,
                UserAgent = sessionOptions?.UserAgent,
                Headers = sessionOptions?.Headers ?? new Dictionary<string, string>(),
                MaxConcurrentPages = Math.Max(1, sessionOptions?.MaxConcurrentFetches ?? 4),
                IdleTimeoutSeconds = Math.Max(1, sessionOptions?.IdleTimeoutSeconds ?? 900)
            },
            cancellationToken).ConfigureAwait(false);

        return _contentPipeline.ExtractFromHtml(html, url, options);
    }

    public void Dispose()
    {
        _searchClient.Dispose();
        _contentPipeline.Dispose();
    }
}
