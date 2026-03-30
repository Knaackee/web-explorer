using FluentAssertions;
using Ndggr.Content.Markdown;
using Xunit;

namespace Ndggr.Tests.Unit.Content;

public class HtmlToMarkdownConverterTests
{
    [Fact]
    public void Convert_SimpleHtml_ProducesMarkdown()
    {
        var html = "<h1>Title</h1><p>Hello <strong>world</strong>.</p>";

        var md = HtmlToMarkdownConverter.Convert(html);

        md.Should().Contain("# Title");
        md.Should().Contain("**world**");
    }

    [Fact]
    public void Convert_ListHtml_ProducesMarkdownList()
    {
        var html = "<ul><li>First</li><li>Second</li><li>Third</li></ul>";

        var md = HtmlToMarkdownConverter.Convert(html);

        md.Should().Contain("- First");
        md.Should().Contain("- Second");
    }

    [Fact]
    public void Convert_LinkHtml_ProducesMarkdownLink()
    {
        var html = "<p>Visit <a href=\"https://example.com\">Example</a>.</p>";

        var md = HtmlToMarkdownConverter.Convert(html);

        md.Should().Contain("[Example](https://example.com)");
    }

    [Fact]
    public void Convert_EmptyString_ReturnsEmpty()
    {
        var md = HtmlToMarkdownConverter.Convert("");
        md.Should().BeEmpty();
    }

    [Fact]
    public void Convert_Null_ReturnsEmpty()
    {
        var md = HtmlToMarkdownConverter.Convert(null!);
        md.Should().BeEmpty();
    }

    [Fact]
    public void Convert_NestedHeadings_PreservesHierarchy()
    {
        var html = "<h1>H1</h1><h2>H2</h2><h3>H3</h3>";

        var md = HtmlToMarkdownConverter.Convert(html);

        md.Should().Contain("# H1");
        md.Should().Contain("## H2");
        md.Should().Contain("### H3");
    }

    [Fact]
    public void Convert_CodeBlock_PreservesCode()
    {
        var html = "<pre><code>var x = 42;</code></pre>";

        var md = HtmlToMarkdownConverter.Convert(html);

        md.Should().Contain("var x = 42;");
    }

    [Fact]
    public void Convert_ExcessiveNewlines_AreCollapsed()
    {
        var html = "<p>First</p><br><br><br><p>Second</p>";

        var md = HtmlToMarkdownConverter.Convert(html);

        md.Should().NotContain("\n\n\n");
    }
}
