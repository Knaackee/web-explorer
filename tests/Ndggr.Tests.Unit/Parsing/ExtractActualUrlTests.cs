using FluentAssertions;
using Ndggr.Parsing;
using Xunit;

namespace Ndggr.Tests.Unit.Parsing;

public class ExtractActualUrlTests
{
    [Theory]
    [InlineData(
        "//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com&rut=abc",
        "https://example.com")]
    [InlineData(
        "//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpath%3Fq%3Dtest&rut=abc",
        "https://example.com/path?q=test")]
    [InlineData(
        "https://duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com&rut=abc",
        "https://example.com")]
    public void ExtractActualUrl_DdgRedirect_ReturnsActualUrl(string input, string expected)
    {
        HtmlResultParser.ExtractActualUrl(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com/direct", "https://example.com/direct")]
    [InlineData("https://example.com/path?q=1", "https://example.com/path?q=1")]
    public void ExtractActualUrl_DirectUrl_ReturnsAsIs(string input, string expected)
    {
        HtmlResultParser.ExtractActualUrl(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("//example.com/page", "https://example.com/page")]
    public void ExtractActualUrl_ProtocolRelative_AddsHttps(string input, string expected)
    {
        HtmlResultParser.ExtractActualUrl(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void ExtractActualUrl_EmptyOrNull_ReturnsEmpty(string? input, string expected)
    {
        HtmlResultParser.ExtractActualUrl(input!).Should().Be(expected);
    }
}
