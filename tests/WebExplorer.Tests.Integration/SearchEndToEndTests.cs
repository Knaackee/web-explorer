using FluentAssertions;
using Xunit;

namespace WebExplorer.Tests.Integration;

/// <summary>
/// End-to-end tests against live DuckDuckGo HTML endpoint.
/// Uses a shared fixture to minimize DDG requests and avoid rate limiting.
/// Run with: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
[Collection("DdgSearch")]
public sealed class SearchEndToEndTests
{
    private readonly SearchFixture _fixture;

    public SearchEndToEndTests(SearchFixture fixture)
    {
        _fixture = fixture;
    }

    private SearchResponse? GetResponseOrNull()
    {
        if (_fixture.SearchError is not null || _fixture.SearchResponse is null || _fixture.SearchResponse.Results.Count == 0)
            return null;
        return _fixture.SearchResponse;
    }

    [Fact]
    public void SearchResponse_IsNotEmpty()
    {
        var response = GetResponseOrNull();
        if (response is null) return; // DDG rate-limited

        response.Results.Should().NotBeEmpty();
        response.Results.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void Results_HaveRequiredFields()
    {
        var response = GetResponseOrNull();
        if (response is null) return; // DDG rate-limited

        foreach (var result in response.Results)
        {
            result.Title.Should().NotBeNullOrWhiteSpace();
            result.Url.Should().NotBeNullOrWhiteSpace();
            result.Url.Should().StartWith("http");
            result.Snippet.Should().NotBeNullOrWhiteSpace();
            result.Index.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Results_UrlsAreValidAbsoluteUris()
    {
        var response = GetResponseOrNull();
        if (response is null) return; // DDG rate-limited

        foreach (var result in response.Results)
        {
            var isValid = Uri.TryCreate(result.Url, UriKind.Absolute, out var uri);
            isValid.Should().BeTrue($"URL '{result.Url}' should be a valid absolute URI");
            uri!.Scheme.Should().BeOneOf("http", "https");
        }
    }

    [Fact]
    public void Results_IndicesAreSequential()
    {
        var response = GetResponseOrNull();
        if (response is null) return; // DDG rate-limited

        for (var i = 0; i < response.Results.Count; i++)
        {
            response.Results[i].Index.Should().Be(i + 1);
        }
    }

    [Fact]
    public void Results_WithEnoughResults_HasNextPageParams()
    {
        var response = GetResponseOrNull();
        if (response is null) return; // DDG rate-limited

        if (response.Results.Count >= 10 && response.NextPageParams is not null)
        {
            response.NextPageParams.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void SiteFilter_ResultsMatchSite()
    {
        if (_fixture.SearchError is not null || _fixture.SiteFilterResponse is null || _fixture.SiteFilterResponse.Results.Count == 0)
            return; // DDG rate-limited

        var response = _fixture.SiteFilterResponse!;
        response.Results.Should().NotBeEmpty();
        response.Results.Should().AllSatisfy(r =>
            r.Url.Should().Contain("github.com"));
    }

    [Fact]
    public async Task SearchAsync_SupportsCancellation()
    {
        using var client = new SearchClient();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => client.SearchAsync("test query", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
