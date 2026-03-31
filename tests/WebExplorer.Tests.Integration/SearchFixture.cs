using Xunit;

namespace WebExplorer.Tests.Integration;

/// <summary>
/// Shared fixture that performs a single DDG search upfront and caches
/// the result for all search-related tests. This avoids DDG rate limiting.
/// </summary>
public sealed class SearchFixture : IAsyncLifetime
{
    public SearchResponse? SearchResponse { get; private set; }
    public SearchResponse? SiteFilterResponse { get; private set; }
    public Exception? SearchError { get; private set; }

    public async Task InitializeAsync()
    {
        using var client = new SearchClient();
        try
        {
            SearchResponse = await RetryHelper.SearchWithRetryAsync(
                () => client.SearchAsync("DuckDuckGo search engine"));

            // Small delay between requests
            await Task.Delay(2000);

            SiteFilterResponse = await RetryHelper.SearchWithRetryAsync(
                () => client.SearchAsync("dotnet runtime", new SearchOptions { Site = "github.com" }));
        }
        catch (Exception ex)
        {
            SearchError = ex;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("DdgSearch")]
public class SearchCollection : ICollectionFixture<SearchFixture>;
