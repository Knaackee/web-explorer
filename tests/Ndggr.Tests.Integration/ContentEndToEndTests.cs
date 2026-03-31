using FluentAssertions;
using Ndggr.Content;
using Ndggr.Content.Models;
using Xunit;

namespace Ndggr.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ContentEndToEndTests : IDisposable
{
    private readonly ContentPipeline _pipeline = new();

    [Fact]
    public async Task ProcessAsync_ExampleDotCom_ExtractsContent()
    {
        var doc = await _pipeline.ProcessAsync("https://example.com");

        doc.Should().NotBeNull();
        doc.Url.Should().Be("https://example.com");
        doc.Markdown.Should().NotBeNullOrWhiteSpace();
        doc.Markdown.Should().Contain("Example Domain");
        doc.FetchedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));
        doc.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_WithMainContentOnly_ExtractsCleanContent()
    {
        var options = new ContentExtractionOptions { MainContentOnly = true };

        var doc = await _pipeline.ProcessAsync("https://example.com", options);

        doc.Markdown.Should().NotBeNullOrWhiteSpace();
        doc.TextContent.Should().NotBeNullOrWhiteSpace();
        doc.WordCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcessAsync_WithChunking_ProducesChunks()
    {
        var options = new ContentExtractionOptions
        {
            MainContentOnly = false,
            ChunkSize = 200,
        };

        var doc = await _pipeline.ProcessAsync("https://example.com", options);

        doc.Chunks.Should().NotBeNull();
        doc.Chunks.Should().NotBeEmpty();

        foreach (var chunk in doc.Chunks!)
        {
            chunk.Id.Should().NotBeNullOrWhiteSpace();
            chunk.Content.Should().NotBeNullOrWhiteSpace();
            chunk.Index.Should().BeGreaterThanOrEqualTo(0);
            chunk.CharCount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ProcessAsync_WithMaxChunks_LimitsOutput()
    {
        var options = new ContentExtractionOptions
        {
            MainContentOnly = false,
            ChunkSize = 100,
            MaxChunks = 2,
        };

        var doc = await _pipeline.ProcessAsync("https://example.com", options);

        doc.Chunks.Should().NotBeNull();
        doc.Chunks!.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ProcessAsync_WithLinks_ExtractsLinks()
    {
        var options = new ContentExtractionOptions
        {
            MainContentOnly = false,
            IncludeLinks = true,
        };

        var doc = await _pipeline.ProcessAsync("https://example.com", options);

        doc.Links.Should().NotBeNull();
        // example.com has at least the "More information..." link
        doc.Links.Should().NotBeEmpty();
        doc.Links!.Should().AllSatisfy(link =>
        {
            link.Href.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task ProcessAsync_RealWebPage_ExtractsTitle()
    {
        // httpbin.org/html has a predictable HTML page
        var doc = await _pipeline.ProcessAsync("https://httpbin.org/html");

        doc.Should().NotBeNull();
        doc.Markdown.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessAsync_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // ContentPipeline wraps TaskCanceledException in ContentFetchException
        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => _pipeline.ProcessAsync("https://example.com", cancellationToken: cts.Token));

        Assert.True(
            ex is OperationCanceledException || ex is ContentFetchException,
            $"Expected OperationCanceledException or ContentFetchException but got {ex.GetType().Name}");
    }

    [Fact]
    public async Task ProcessAsync_InvalidUrl_ThrowsContentFetchException()
    {
        var act = () => _pipeline.ProcessAsync("https://this-domain-definitely-does-not-exist-12345.com");

        await act.Should().ThrowAsync<ContentFetchException>();
    }

    [Fact]
    public async Task ProcessAsync_ChunkIdsAreDeterministic()
    {
        var options = new ContentExtractionOptions
        {
            MainContentOnly = false,
            ChunkSize = 200,
        };

        var doc1 = await _pipeline.ProcessAsync("https://example.com", options);
        var doc2 = await _pipeline.ProcessAsync("https://example.com", options);

        doc1.Chunks.Should().NotBeNull();
        doc2.Chunks.Should().NotBeNull();
        doc1.Chunks!.Select(c => c.Id).Should().BeEquivalentTo(doc2.Chunks!.Select(c => c.Id));
    }

    [Fact]
    public async Task ProcessAsync_FullPipeline_ReturnsCompleteDocument()
    {
        var options = new ContentExtractionOptions
        {
            MainContentOnly = true,
            IncludeLinks = true,
            ChunkSize = 500,
        };

        var doc = await _pipeline.ProcessAsync("https://example.com", options);

        doc.SchemaVersion.Should().Be(1);
        doc.Url.Should().NotBeNullOrWhiteSpace();
        doc.Markdown.Should().NotBeNullOrWhiteSpace();
        doc.FetchedAt.Should().NotBe(default);
        doc.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_JsonSerializationRoundtrip()
    {
        var options = new ContentExtractionOptions
        {
            MainContentOnly = false,
            IncludeLinks = true,
            ChunkSize = 300,
        };

        var doc = await _pipeline.ProcessAsync("https://example.com", options);

        var json = System.Text.Json.JsonSerializer.Serialize(doc);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ContentDocument>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Url.Should().Be(doc.Url);
        deserialized.Markdown.Should().Be(doc.Markdown);
        deserialized.SchemaVersion.Should().Be(doc.SchemaVersion);
    }

    public void Dispose() => _pipeline.Dispose();
}
