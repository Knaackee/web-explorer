using FluentAssertions;
using Ndggr.Content;
using Ndggr.Content.Models;
using Xunit;

namespace Ndggr.Tests.Unit.Content;

/// <summary>
/// Golden-file tests: verify extraction quality across different content types.
/// Each fixture represents a common web-page archetype.
/// </summary>
public class GoldenFileExtractionTests
{
    private static readonly string NewsHtml =
        File.ReadAllText(Path.Combine("Fixtures", "golden_news_article.html"));

    private static readonly string DocsHtml =
        File.ReadAllText(Path.Combine("Fixtures", "golden_docs_api.html"));

    private static readonly string BlogHtml =
        File.ReadAllText(Path.Combine("Fixtures", "golden_blog_post.html"));

    private static readonly string WikiHtml =
        File.ReadAllText(Path.Combine("Fixtures", "golden_wiki_article.html"));

    private static readonly string GitHubHtml =
        File.ReadAllText(Path.Combine("Fixtures", "golden_github_readme.html"));

    // ── News Article ──

    [Fact]
    public void News_ExtractsTitle()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(NewsHtml, "https://newsdaily.example.com/article/123");

        doc.Title.Should().NotBeNullOrEmpty();
        doc.Title.Should().Contain("Market");
    }

    [Fact]
    public void News_ExtractsMainContent_ExcludesNavAndFooter()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(NewsHtml, "https://newsdaily.example.com/article/123");

        doc.Markdown.Should().Contain("Federal Reserve");
        doc.Markdown.Should().Contain("Key Market Movements");
        // Navigation and ads should be stripped by Readability
        doc.Markdown.Should().NotContain("Privacy Policy");
        doc.Markdown.Should().NotContain("Advertisement");
    }

    [Fact]
    public void News_HasReasonableWordCount()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(NewsHtml, "https://newsdaily.example.com/article/123");

        doc.WordCount.Should().BeGreaterThan(100);
        doc.WordCount.Should().BeLessThan(2000);
    }

    [Fact]
    public void News_PreservesBlockquote()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(NewsHtml, "https://newsdaily.example.com/article/123");

        // Blockquote content should be preserved in markdown
        doc.Markdown.Should().Contain("Committee decided to maintain");
    }

    // ── API Documentation ──

    [Fact]
    public void Docs_ExtractsTitle()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(DocsHtml, "https://learn.microsoft.com/dotnet/api/concurrentdictionary");

        doc.Title.Should().NotBeNullOrEmpty();
        doc.Title.Should().Contain("ConcurrentDictionary");
    }

    [Fact]
    public void Docs_PreservesCodeBlocks()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(DocsHtml, "https://learn.microsoft.com/dotnet/api/concurrentdictionary");

        // Code content should be preserved
        doc.Markdown.Should().Contain("TKey");
        doc.Markdown.Should().Contain("TValue");
    }

    [Fact]
    public void Docs_ExtractsMethodNames()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(DocsHtml, "https://learn.microsoft.com/dotnet/api/concurrentdictionary");

        doc.Markdown.Should().Contain("TryAdd");
        doc.Markdown.Should().Contain("TryGetValue");
        doc.Markdown.Should().Contain("GetOrAdd");
    }

    [Fact]
    public void Docs_PreservesTableStructure()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(DocsHtml, "https://learn.microsoft.com/dotnet/api/concurrentdictionary");

        // Tables should produce some markdown (GFM tables or text)
        doc.Markdown.Should().Contain("Constructor");
        doc.Markdown.Should().Contain("Description");
    }

    // ── Blog Post ──

    [Fact]
    public void Blog_ExtractsTitle()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(BlogHtml, "https://devblog.example.com/rest-api");

        doc.Title.Should().NotBeNullOrEmpty();
        doc.Title.Should().Contain("REST API");
    }

    [Fact]
    public void Blog_ExtractsSections()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(BlogHtml, "https://devblog.example.com/rest-api");

        doc.Markdown.Should().Contain("Prerequisites");
        doc.Markdown.Should().Contain("Project Setup");
        doc.Markdown.Should().Contain("Conclusion");
    }

    [Fact]
    public void Blog_PreservesCodeSnippets()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(BlogHtml, "https://devblog.example.com/rest-api");

        doc.Markdown.Should().Contain("dotnet new webapi");
        doc.Markdown.Should().Contain("TodoItem");
    }

    [Fact]
    public void Blog_ExcludesSidebarAndComments()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(BlogHtml, "https://devblog.example.com/rest-api");

        // Sidebar content should be stripped
        doc.Markdown.Should().NotContain("KubeAcademy");
    }

    // ── Wikipedia-style ──

    [Fact]
    public void Wiki_ExtractsTitle()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(WikiHtml, "https://en.example.org/wiki/Quantum_Computing");

        doc.Title.Should().NotBeNullOrEmpty();
        doc.Title.Should().Contain("Quantum");
    }

    [Fact]
    public void Wiki_ExtractsAllSections()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(WikiHtml, "https://en.example.org/wiki/Quantum_Computing");

        doc.Markdown.Should().Contain("History");
        doc.Markdown.Should().Contain("Principles");
        doc.Markdown.Should().Contain("Algorithms");
        doc.Markdown.Should().Contain("Applications");
    }

    [Fact]
    public void Wiki_HasSubstantialContent()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(WikiHtml, "https://en.example.org/wiki/Quantum_Computing");

        doc.WordCount.Should().BeGreaterThan(300);
    }

    [Fact]
    public void Wiki_PreservesBoldAndLinks()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(WikiHtml, "https://en.example.org/wiki/Quantum_Computing");

        // Bold text and link text should survive
        doc.Markdown.Should().Contain("Shor's algorithm");
        doc.Markdown.Should().Contain("Grover's algorithm");
    }

    // ── GitHub README ──

    [Fact]
    public void GitHub_ExtractsTitle()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(GitHubHtml, "https://github.com/dotnet/runtime");

        doc.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GitHub_ExtractsComponents()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(GitHubHtml, "https://github.com/dotnet/runtime");

        doc.Markdown.Should().Contain("CoreCLR");
        doc.Markdown.Should().Contain("Mono");
        doc.Markdown.Should().Contain("NativeAOT");
    }

    [Fact]
    public void GitHub_PreservesBuildInstructions()
    {
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(GitHubHtml, "https://github.com/dotnet/runtime");

        doc.Markdown.Should().Contain("build.cmd");
        doc.Markdown.Should().Contain("build.sh");
    }

    // ── Cross-archetype: common contract ──

    [Theory]
    [InlineData("golden_news_article.html", "https://example.com/news")]
    [InlineData("golden_docs_api.html", "https://example.com/docs")]
    [InlineData("golden_blog_post.html", "https://example.com/blog")]
    [InlineData("golden_wiki_article.html", "https://example.com/wiki")]
    [InlineData("golden_github_readme.html", "https://example.com/readme")]
    public void AllFixtures_ProduceNonEmptyMarkdown(string fixture, string url)
    {
        var html = File.ReadAllText(Path.Combine("Fixtures", fixture));
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(html, url);

        doc.Markdown.Should().NotBeNullOrEmpty("every golden fixture must produce markdown");
        doc.Url.Should().Be(url);
        doc.SchemaVersion.Should().Be(1);
    }

    [Theory]
    [InlineData("golden_news_article.html", "https://example.com/news")]
    [InlineData("golden_docs_api.html", "https://example.com/docs")]
    [InlineData("golden_blog_post.html", "https://example.com/blog")]
    [InlineData("golden_wiki_article.html", "https://example.com/wiki")]
    [InlineData("golden_github_readme.html", "https://example.com/readme")]
    public void AllFixtures_ProducePositiveWordCount(string fixture, string url)
    {
        var html = File.ReadAllText(Path.Combine("Fixtures", fixture));
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(html, url);

        doc.WordCount.Should().BeGreaterThan(0, "every golden fixture must have extractable words");
    }

    [Theory]
    [InlineData("golden_docs_api.html", "https://example.com/docs")]
    [InlineData("golden_blog_post.html", "https://example.com/blog")]
    [InlineData("golden_wiki_article.html", "https://example.com/wiki")]
    [InlineData("golden_github_readme.html", "https://example.com/readme")]
    public void LinkRichFixtures_ExtractLinks(string fixture, string url)
    {
        var html = File.ReadAllText(Path.Combine("Fixtures", fixture));
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { IncludeLinks = true };
        var doc = pipeline.ExtractFromHtml(html, url, options);

        doc.Links.Should().NotBeNull();
        doc.Links!.Count.Should().BeGreaterThan(0, "link-rich fixtures should contain at least one link");
    }

    [Fact]
    public void AllFixtures_LinksExtractable_WithFullHtml()
    {
        // When MainContentOnly=false, links are extracted from the full HTML
        var html = File.ReadAllText(Path.Combine("Fixtures", "golden_news_article.html"));
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { IncludeLinks = true, MainContentOnly = false };
        var doc = pipeline.ExtractFromHtml(html, "https://example.com/news", options);

        doc.Links.Should().NotBeNull();
        doc.Links!.Count.Should().BeGreaterThan(0, "full HTML should always have links");
    }

    [Theory]
    [InlineData("golden_news_article.html", "https://example.com/news")]
    [InlineData("golden_docs_api.html", "https://example.com/docs")]
    [InlineData("golden_blog_post.html", "https://example.com/blog")]
    [InlineData("golden_wiki_article.html", "https://example.com/wiki")]
    [InlineData("golden_github_readme.html", "https://example.com/readme")]
    public void AllFixtures_ChunkableWithStableIds(string fixture, string url)
    {
        var html = File.ReadAllText(Path.Combine("Fixtures", fixture));
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { ChunkSize = 500 };

        var doc1 = pipeline.ExtractFromHtml(html, url, options);
        var doc2 = pipeline.ExtractFromHtml(html, url, options);

        doc1.Chunks.Should().NotBeNull();
        doc1.Chunks!.Count.Should().BeGreaterThan(0);
        doc1.Chunks.Select(c => c.Id).Should().BeEquivalentTo(doc2.Chunks!.Select(c => c.Id),
            "chunking must be deterministic across runs");
    }
}
