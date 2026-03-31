using Xunit;

namespace Ndggr.Tests.Integration;

/// <summary>
/// Shared fixture that performs a single DDG search upfront and caches
/// the result for all search-related tests. This avoids DDG rate limiting.
/// </summary>
public sealed class DdgSearchFixture : IAsyncLifetime
{
    public DdgSearchResponse? SearchResponse { get; private set; }
    public DdgSearchResponse? SiteFilterResponse { get; private set; }
    public Exception? SearchError { get; private set; }

    public async Task InitializeAsync()
    {
        using var client = new DdgClient();
        try
        {
            SearchResponse = await RetryHelper.SearchWithRetryAsync(
                () => client.SearchAsync("DuckDuckGo search engine"));

            // Small delay between requests
            await Task.Delay(2000);

            SiteFilterResponse = await RetryHelper.SearchWithRetryAsync(
                () => client.SearchAsync("dotnet runtime", new DdgSearchOptions { Site = "github.com" }));
        }
        catch (Exception ex)
        {
            SearchError = ex;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("DdgSearch")]
public class DdgSearchCollection : ICollectionFixture<DdgSearchFixture>;
