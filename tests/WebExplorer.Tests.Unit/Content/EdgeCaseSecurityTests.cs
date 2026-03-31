using FluentAssertions;
using WebExplorer.Content;
using WebExplorer.Content.Extraction;
using WebExplorer.Content.Markdown;
using Xunit;

namespace WebExplorer.Tests.Unit.Content;

/// <summary>
/// Edge-case and security tests: XSS sanitization, unusual inputs, parser robustness.
/// </summary>
public class EdgeCaseSecurityTests
{
    // ── XSS / script injection ──

    [Fact]
    public void Markdown_DoesNotContainScriptTags()
    {
        const string xssHtml = """
            <html><body>
            <article>
            <p>Safe content</p>
            <script>alert('XSS')</script>
            <p>More safe content</p>
            </article>
            </body></html>
            """;
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(xssHtml, "https://example.com/xss");

        doc.Markdown.Should().NotContain("<script>");
        doc.Markdown.Should().NotContain("alert(");
    }

    [Fact]
    public void Markdown_DoesNotContainEventHandlers()
    {
        const string eventHtml = """
            <html><body>
            <article>
            <div onmouseover="alert('xss')">Hover me</div>
            <img src="x" onerror="alert('xss')">
            <p onclick="steal()">Click me</p>
            </article>
            </body></html>
            """;
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(eventHtml, "https://example.com/events");

        doc.Markdown.Should().NotContain("onmouseover");
        doc.Markdown.Should().NotContain("onerror");
        doc.Markdown.Should().NotContain("onclick");
    }

    [Fact]
    public void Links_DoNotContainJavascriptUrls()
    {
        const string jsLinkHtml = """
            <html><body>
            <a href="javascript:alert('xss')">Click</a>
            <a href="https://safe.example.com">Safe</a>
            <a href="JAVASCRIPT:void(0)">Also bad</a>
            </body></html>
            """;
        var links = LinkExtractor.Extract(jsLinkHtml, "https://example.com");

        links.Should().AllSatisfy(l =>
            l.Href.Should().NotStartWith("javascript:", "javascript: URLs must be filtered"));
    }

    [Fact]
    public void Markdown_StripsStyleTags()
    {
        const string styleHtml = """
            <html><body>
            <style>.hidden { display: none; }</style>
            <article><p>Visible content</p></article>
            </body></html>
            """;
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(styleHtml, "https://example.com/style");

        doc.Markdown.Should().NotContain("display: none");
        doc.Markdown.Should().NotContain("<style>");
    }

    // ── Unicode edge cases ──

    [Fact]
    public void Extract_HandlesRtlContent()
    {
        const string rtlHtml = """
            <html dir="rtl" lang="ar"><body>
            <article><p>مرحبا بالعالم</p><p>This is mixed content</p></article>
            </body></html>
            """;
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(rtlHtml, "https://example.com/rtl");

        doc.Should().NotBeNull();
        doc.Markdown.Should().Contain("مرحبا");
    }

    [Fact]
    public void Extract_HandlesZeroWidthCharacters()
    {
        const string zwHtml = "<html><body><article><p>Hello\u200B\u200CWorld\u200D\uFEFF</p></article></body></html>";
        using var pipeline = new ContentPipeline();
        var doc = pipeline.ExtractFromHtml(zwHtml, "https://example.com/zw");

        doc.Should().NotBeNull();
        doc.Markdown.Should().Contain("Hello");
        doc.Markdown.Should().Contain("World");
    }

    // ── Markdown converter edge cases ──

    [Fact]
    public void MarkdownConverter_HandlesEmptyString()
    {
        var result = HtmlToMarkdownConverter.Convert("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void MarkdownConverter_HandlesOnlyWhitespace()
    {
        var result = HtmlToMarkdownConverter.Convert("   \n\n  ");
        result.Should().BeEmpty();
    }

    [Fact]
    public void MarkdownConverter_CollapsesExcessiveBlankLines()
    {
        const string html = "<p>One</p><br><br><br><br><br><p>Two</p>";

        var result = HtmlToMarkdownConverter.Convert(html);

        result.Should().NotContain("\n\n\n");
    }

    // ── Link extractor edge cases ──

    [Fact]
    public void LinkExtractor_IgnoresFragmentLinks()
    {
        const string html = """
            <html><body>
            <a href="#section">Internal</a>
            <a href="https://example.com">External</a>
            </body></html>
            """;
        var links = LinkExtractor.Extract(html, "https://example.com");

        links.Should().NotContain(l => l.Href.StartsWith("#"));
    }

    [Fact]
    public void LinkExtractor_ResolvesRelativeUrls()
    {
        const string html = """
            <html><body>
            <a href="/about">About</a>
            <a href="page.html">Page</a>
            </body></html>
            """;
        var links = LinkExtractor.Extract(html, "https://example.com/blog/post");

        links.Should().Contain(l => l.Href == "https://example.com/about");
        links.Should().Contain(l => l.Href == "https://example.com/blog/page.html");
    }

    [Fact]
    public void LinkExtractor_HandlesEmptyHrefs()
    {
        const string html = """
            <html><body>
            <a href="">Empty</a>
            <a>No href at all</a>
            <a href="https://valid.com">Valid</a>
            </body></html>
            """;
        var links = LinkExtractor.Extract(html, "https://example.com");

        links.Should().Contain(l => l.Href == "https://valid.com/");
    }

    // ── ContentExtractionOptions edge cases ──

    [Fact]
    public void Options_DefaultValues_AreCorrect()
    {
        var options = new ContentExtractionOptions();

        options.MainContentOnly.Should().BeTrue();
        options.IncludeLinks.Should().BeFalse();
        options.ChunkSize.Should().Be(0);
        options.MaxChunks.Should().Be(0);
        options.TimeoutMs.Should().Be(30_000);
        options.MaxRetries.Should().Be(2);
        options.Proxy.Should().BeNull();
        options.UserAgent.Should().BeNull();
        options.Headers.Should().BeEmpty();
        options.SchemaVersion.Should().Be(1);
    }

    // ── Exception hierarchy ──

    [Fact]
    public void ContentFetchException_CarriesStatusCode()
    {
        var ex = new ContentFetchException("Not found", 404);
        ex.StatusCode.Should().Be(404);
        ex.Message.Should().Contain("Not found");
        ex.Should().BeAssignableTo<WebExplorerException>();
    }

    [Fact]
    public void ContentExtractionException_CarriesInnerException()
    {
        var inner = new InvalidOperationException("parse failed");
        var ex = new ContentExtractionException("Extraction failed", inner);
        ex.InnerException.Should().Be(inner);
        ex.Should().BeAssignableTo<WebExplorerException>();
    }

    [Fact]
    public void ContentFetchException_WithoutStatusCode_HasNullStatusCode()
    {
        var ex = new ContentFetchException("Generic error");
        ex.StatusCode.Should().BeNull();
    }
}
