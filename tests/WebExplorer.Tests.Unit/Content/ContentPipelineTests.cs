using FluentAssertions;
using WebExplorer.Content;
using WebExplorer.Content.Models;
using Xunit;

namespace WebExplorer.Tests.Unit.Content;

public class ContentPipelineTests
{
    private static readonly string ArticleHtml =
        File.ReadAllText(Path.Combine("Fixtures", "content_article.html"));

    private static readonly string MinimalHtml =
        File.ReadAllText(Path.Combine("Fixtures", "content_minimal.html"));

    [Fact]
    public void ExtractFromHtml_Article_ExtractsTitle()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.Title.Should().NotBeNullOrEmpty();
        doc.Title.Should().Contain("Test Article");
    }

    [Fact]
    public void ExtractFromHtml_Article_ExtractsMarkdown()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.Markdown.Should().NotBeNullOrEmpty();
        doc.Markdown.Should().Contain("JIT Compilation");
        doc.Markdown.Should().Contain("Garbage Collection");
    }

    [Fact]
    public void ExtractFromHtml_Article_HasWordCount()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.WordCount.Should().BeGreaterThan(50);
    }

    [Fact]
    public void ExtractFromHtml_Article_HasUrl()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.Url.Should().Be("https://example.com/article");
    }

    [Fact]
    public void ExtractFromHtml_Article_HasSchemaVersion()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void ExtractFromHtml_Article_HasFetchedAt()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.FetchedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ExtractFromHtml_WithChunking_ProducesChunks()
    {
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { ChunkSize = 200 };
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article", options);

        doc.Chunks.Should().NotBeNull();
        doc.Chunks!.Count.Should().BeGreaterThan(1);
        doc.Chunks.All(c => c.Id.Length == 12).Should().BeTrue();
        doc.Chunks.All(c => c.Content.Length > 0).Should().BeTrue();
    }

    [Fact]
    public void ExtractFromHtml_WithMaxChunks_LimitsOutput()
    {
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { ChunkSize = 200, MaxChunks = 2 };
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article", options);

        doc.Chunks.Should().NotBeNull();
        doc.Chunks!.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void ExtractFromHtml_WithLinks_ExtractsLinks()
    {
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { IncludeLinks = true };
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article", options);

        doc.Links.Should().NotBeNull();
        doc.Links!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExtractFromHtml_WithoutChunking_NoChunks()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.Chunks.Should().BeNull();
    }

    [Fact]
    public void ExtractFromHtml_WithoutLinks_NoLinks()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article");

        doc.Links.Should().BeNull();
    }

    [Fact]
    public void ExtractFromHtml_NotMainContentOnly_UsesFullHtml()
    {
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { MainContentOnly = false };
        var doc = pipeline.ExtractFromHtml(ArticleHtml, "https://example.com/article", options);

        // Full HTML includes footer copyright text (non-link content from outside <article>)
        doc.Markdown.Should().Contain("2025 Example Blog");
    }

    [Fact]
    public void ExtractFromHtml_MinimalPage_StillProducesOutput()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(MinimalHtml, "https://example.com/minimal");

        doc.Markdown.Should().NotBeNullOrEmpty();
        doc.Url.Should().Be("https://example.com/minimal");
    }

    [Fact]
    public void ExtractFromHtml_EmptyUrl_Throws()
    {
        using var pipeline = new ContentPipeline();

        var act = () => pipeline.ExtractFromHtml("<html></html>", "");

        act.Should().Throw<Exception>();
    }
}
