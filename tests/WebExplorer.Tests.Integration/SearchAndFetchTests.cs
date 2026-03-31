using WebExplorer.Content;
using WebExplorer.Content.Models;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace WebExplorer.Tests.Integration;

/// <summary>
/// Searches DDG, then fetches content from every result URL.
/// Writes a full report to tests/WebExplorer.Tests.Integration/output/search_and_fetch_report.txt
/// Run with: dotnet test --filter "FullyQualifiedName~SearchAndFetchTests" --no-build
/// </summary>
[Trait("Category", "Integration")]
public sealed class SearchAndFetchTests : IDisposable
{
    private readonly ContentPipeline _pipeline = new();
    private readonly ITestOutputHelper _output;

    public SearchAndFetchTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SearchAndFetchAll_WritesReport()
    {
        var query = "werder bremen";

        // 1. Search
        using var client = new SearchClient();
        var searchResponse = await RetryHelper.SearchWithRetryAsync(
            () => client.SearchAsync(query));

        var sb = new StringBuilder();
        sb.AppendLine($"Search query: {query}");
        sb.AppendLine($"Timestamp:    {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"Results:      {searchResponse.Results.Count}");

        if (searchResponse.InstantAnswer is not null)
        {
            sb.AppendLine();
            sb.AppendLine("=== INSTANT ANSWER ===");
            sb.AppendLine(searchResponse.InstantAnswer.Text);
            if (searchResponse.InstantAnswer.Url is not null)
                sb.AppendLine($"URL: {searchResponse.InstantAnswer.Url}");
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', 80));

        // 2. Fetch each result
        var options = new ContentExtractionOptions { MainContentOnly = true };
        var fetchTimeout = TimeSpan.FromSeconds(15);

        foreach (var result in searchResponse.Results)
        {
            sb.AppendLine();
            sb.AppendLine($"--- [{result.Index}] {result.Title} ---");
            sb.AppendLine($"URL:     {result.Url}");
            sb.AppendLine($"Snippet: {result.Snippet}");
            sb.AppendLine();

            try
            {
                using var cts = new CancellationTokenSource(fetchTimeout);
                var doc = await _pipeline.ProcessAsync(result.Url, options, cts.Token);

                sb.AppendLine("Status:    OK");
                sb.AppendLine($"Words:     {doc.WordCount}");
                sb.AppendLine($"Fetched:   {doc.FetchedAt:O}");
                sb.AppendLine($"Links:     {doc.Links?.Count ?? 0}");
                sb.AppendLine();
                sb.AppendLine("--- MARKDOWN (first 2000 chars) ---");
                var md = doc.Markdown ?? "";
                sb.AppendLine(md.Length > 2000
                    ? md[..2000] + "\n[...truncated...]"
                    : md);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Status:    FAILED");
                sb.AppendLine($"Error:     {ex.GetType().Name}: {ex.Message}");
            }

            sb.AppendLine();
            sb.AppendLine(new string('-', 80));
        }

        // 3. Write output file
        var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "WebExplorer.Tests.Integration", "output");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "search_and_fetch_report.txt");
        await File.WriteAllTextAsync(outputPath, sb.ToString());

        _output.WriteLine($"Report written to: {outputPath}");
        _output.WriteLine($"Results found: {searchResponse.Results.Count}");

        // Basic assertion so the test shows green
        Assert.True(searchResponse.Results.Count > 0, "Search returned no results");
    }

    public void Dispose() => _pipeline.Dispose();
}
