using FluentAssertions;
using WebExplorer.Content;
using WebExplorer.Extensions;
using WebExplorer.Playwright;
using Xunit;

namespace WebExplorer.Tests.Integration;

/// <summary>
/// Tests the WebExplorerClient facade. Search tests use the shared fixture to avoid DDG rate limiting.
/// Fetch tests use example.com which is always available.
/// </summary>
[Trait("Category", "Integration")]
[Collection("DdgSearch")]
public sealed class FacadeEndToEndTests : IDisposable
{
    private readonly WebExplorerClient _client = new();
    private readonly SearchFixture _fixture;

    public FacadeEndToEndTests(SearchFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void SearchAsync_ReturnsSearchResults()
    {
        if (_fixture.SearchError is not null || _fixture.SearchResponse is null || _fixture.SearchResponse.Results.Count == 0)
            return; // DDG rate-limited

        // Verify the facade returns the same kind of response as SearchClient
        var response = _fixture.SearchResponse!;
        response.Should().NotBeNull();
        response.Results.Should().NotBeEmpty();
        response.Results[0].Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FetchMarkdownAsync_ReturnsMarkdownString()
    {
        var markdown = await _client.FetchMarkdownAsync("https://example.com");

        markdown.Should().NotBeNullOrWhiteSpace();
        markdown.Should().Contain("Example Domain");
    }

    [Fact]
    public async Task FetchAsync_ReturnsStructuredDocument()
    {
        var doc = await _client.FetchAsync("https://example.com");

        doc.Should().NotBeNull();
        doc.Url.Should().Be("https://example.com");
        doc.Markdown.Should().NotBeNullOrWhiteSpace();
        doc.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public async Task FetchAsync_WithOptions_AppliesOptions()
    {
        var options = new ContentExtractionOptions
        {
            IncludeLinks = true,
            MainContentOnly = false,
        };

        var doc = await _client.FetchAsync("https://example.com", options);

        doc.Links.Should().NotBeNull();
        doc.Links.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchThenFetch_FullWorkflow()
    {
        if (_fixture.SearchError is not null || _fixture.SearchResponse is null || _fixture.SearchResponse.Results.Count == 0)
            return; // DDG rate-limited

        var searchResponse = _fixture.SearchResponse!;
        searchResponse.Results.Should().NotBeEmpty();

        // Fetch the first result's URL
        var firstUrl = searchResponse.Results[0].Url;
        firstUrl.Should().NotBeNullOrWhiteSpace();

        var doc = await _client.FetchAsync(firstUrl);

        doc.Should().NotBeNull();
        doc.Url.Should().NotBeNullOrWhiteSpace();
        doc.Markdown.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Constructor_WithBothOptions_WorksCorrectly()
    {
        using var client = new WebExplorerClient(
            new SearchOptions { Region = "us-en" },
            new ContentExtractionOptions { MainContentOnly = true });

        var markdown = await client.FetchMarkdownAsync("https://example.com");

        markdown.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlaywrightSession_FetchesPreserveCookiesAcrossRequests()
    {
        if (!await PlaywrightTestSupport.IsChromiumAvailableAsync())
            return;

        using var server = new CookieTestServer();
        var sessionId = $"facade-{Guid.NewGuid():N}"[..15];

        try
        {
            var session = await _client.StartPlaywrightSessionAsync(new PlaywrightSessionStartOptions
            {
                SessionId = sessionId,
                IdleTimeoutSeconds = 600,
            });

            session.SessionId.Should().Be(sessionId);

            await _client.FetchWithPlaywrightSessionAsync(sessionId, server.SetCookieUrl);
            var cookieDoc = await _client.FetchWithPlaywrightSessionAsync(sessionId, server.EchoCookieUrl);

            cookieDoc.Markdown.Should().Contain("wxp-session=retained");

            var inspect = await _client.GetPlaywrightSessionAsync(sessionId);
            inspect.Should().NotBeNull();
            inspect!.State.Should().Be(PlaywrightSessionState.Active);
        }
        finally
        {
            await _client.EndPlaywrightSessionAsync(sessionId);
        }

        var removed = await _client.GetPlaywrightSessionAsync(sessionId);
        removed.Should().BeNull();
    }

    [Fact]
    public async Task PlaywrightPersistentSession_CanResumeAndPreserveCookies()
    {
        if (!await PlaywrightTestSupport.IsChromiumAvailableAsync())
            return;

        using var server = new CookieTestServer();
        var sessionId = $"resume-{Guid.NewGuid():N}"[..15];

        try
        {
            await _client.StartPlaywrightSessionAsync(new PlaywrightSessionStartOptions
            {
                SessionId = sessionId,
                Persistent = true,
                IdleTimeoutSeconds = 600,
            });

            await _client.FetchWithPlaywrightSessionAsync(sessionId, server.SetCookieUrl);
            await _client.EndPlaywrightSessionAsync(sessionId);

            var stopped = await _client.GetPlaywrightSessionAsync(sessionId);
            stopped.Should().NotBeNull();
            stopped!.State.Should().Be(PlaywrightSessionState.Stopped);

            await _client.ResumePlaywrightSessionAsync(sessionId);
            var cookieDoc = await _client.FetchWithPlaywrightSessionAsync(sessionId, server.EchoCookieUrl);
            cookieDoc.Markdown.Should().Contain("wxp-session=retained");
        }
        finally
        {
            await _client.EndPlaywrightSessionAsync(sessionId, deleteSessionData: true);
        }

        var removed = await _client.GetPlaywrightSessionAsync(sessionId);
        removed.Should().BeNull();
    }

    [Fact]
    public async Task PlaywrightSession_MaxConcurrentFetches_IsEnforced()
    {
        if (!await PlaywrightTestSupport.IsChromiumAvailableAsync())
            return;

        using var server = new CookieTestServer();
        var sessionId = $"slots-{Guid.NewGuid():N}"[..15];

        try
        {
            await _client.StartPlaywrightSessionAsync(new PlaywrightSessionStartOptions
            {
                SessionId = sessionId,
                IdleTimeoutSeconds = 600,
                MaxConcurrentFetches = 1,
            });

            var first = _client.FetchWithPlaywrightSessionAsync(sessionId, server.DelayedResponseUrl);
            var second = _client.FetchWithPlaywrightSessionAsync(sessionId, server.DelayedResponseUrl);

            await Task.WhenAll(first, second);
            server.MaxObservedConcurrentDelayRequests.Should().Be(1);
        }
        finally
        {
            await _client.EndPlaywrightSessionAsync(sessionId, deleteSessionData: true);
        }
    }

    [Fact]
    public async Task PlaywrightSharedBrowserPool_ReusesBrowserAcrossFetches()
    {
        if (!await PlaywrightTestSupport.IsChromiumAvailableAsync())
            return;

        using var server = new CookieTestServer();
        var pool = new PlaywrightSharedBrowserPool();
        var options = new PlaywrightSharedBrowserOptions
        {
            IdleTimeoutSeconds = 600,
            MaxConcurrentPages = 2
        };

        try
        {
            var initial = await pool.GetBrowserInfoAsync(options);
            initial.Should().BeNull();

            await pool.FetchHtmlAsync(server.SetCookieUrl, browserOptions: options);
            var firstInfo = await pool.GetBrowserInfoAsync(options);
            firstInfo.Should().NotBeNull();

            await pool.FetchHtmlAsync(server.EchoCookieUrl, browserOptions: options);
            var secondInfo = await pool.GetBrowserInfoAsync(options);
            secondInfo.Should().NotBeNull();
            secondInfo!.ProcessId.Should().Be(firstInfo!.ProcessId);
            secondInfo.ConfigHash.Should().Be(firstInfo.ConfigHash);
        }
        finally
        {
            await pool.StopBrowserAsync(options, deleteBrowserData: true);
        }
    }

    public void Dispose() => _client.Dispose();
}
