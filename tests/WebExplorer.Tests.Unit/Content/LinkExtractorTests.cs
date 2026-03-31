using FluentAssertions;
using WebExplorer.Content.Extraction;
using Xunit;

namespace WebExplorer.Tests.Unit.Content;

public class LinkExtractorTests
{
    [Fact]
    public void Extract_FindsLinks()
    {
        var html = """
            <html><body>
            <p>Visit <a href="https://example.com">Example</a> and <a href="https://test.com">Test</a>.</p>
            </body></html>
            """;

        var links = LinkExtractor.Extract(html, "https://base.com");

        links.Should().HaveCount(2);
        links[0].Href.Should().Be("https://example.com/");
        links[0].Text.Should().Be("Example");
    }

    [Fact]
    public void Extract_ResolvesRelativeUrls()
    {
        var html = """<html><body><a href="/about">About</a></body></html>""";

        var links = LinkExtractor.Extract(html, "https://example.com/page");

        links.Should().HaveCount(1);
        links[0].Href.Should().Be("https://example.com/about");
    }

    [Fact]
    public void Extract_SkipsFragmentLinks()
    {
        var html = """<html><body><a href="#section">Jump</a></body></html>""";

        var links = LinkExtractor.Extract(html, "https://example.com");

        links.Should().BeEmpty();
    }

    [Fact]
    public void Extract_SkipsJavascriptLinks()
    {
        var html = """<html><body><a href="javascript:void(0)">Click</a></body></html>""";

        var links = LinkExtractor.Extract(html, "https://example.com");

        links.Should().BeEmpty();
    }

    [Fact]
    public void Extract_EmptyHtml_ReturnsEmpty()
    {
        var links = LinkExtractor.Extract("<html><body></body></html>", "https://example.com");

        links.Should().BeEmpty();
    }
}
