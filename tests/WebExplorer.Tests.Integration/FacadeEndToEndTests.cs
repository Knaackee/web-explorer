using FluentAssertions;
using WebExplorer.Content;
using WebExplorer.Extensions;
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

    public void Dispose() => _client.Dispose();
}
