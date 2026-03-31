using FluentAssertions;
using WebExplorer.Parsing;
using Xunit;

namespace WebExplorer.Tests.Unit.Parsing;

public class HtmlResultParserTests
{
    private readonly HtmlResultParser _parser = new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine("Fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_BasicResults_ReturnsExpectedCount()
    {
        var html = LoadFixture("search_results_basic.html");

        var response = _parser.Parse(html);

        response.Results.Should().HaveCount(5);
    }

    [Fact]
    public void Parse_BasicResults_FirstResultHasCorrectFields()
    {
        var html = LoadFixture("search_results_basic.html");

        var response = _parser.Parse(html);
        var first = response.Results[0];

        first.Index.Should().Be(1);
        first.Title.Should().Be(".NET - Wikipedia");
        first.Url.Should().Be("https://en.wikipedia.org/wiki/.NET");
        first.Snippet.Should().Contain("free and open-source");
    }

    [Fact]
    public void Parse_BasicResults_ExtractsAllUrls()
    {
        var html = LoadFixture("search_results_basic.html");

        var response = _parser.Parse(html);

        response.Results[0].Url.Should().Be("https://en.wikipedia.org/wiki/.NET");
        response.Results[1].Url.Should().Be("https://dotnet.microsoft.com/");
        response.Results[2].Url.Should().Be("https://learn.microsoft.com/en-us/dotnet/");
        response.Results[3].Url.Should().Be("https://github.com/dotnet/runtime");
        response.Results[4].Url.Should().Be("https://stackoverflow.com/questions/tagged/.net");
    }

    [Fact]
    public void Parse_BasicResults_ExtractsNavigationTokens()
    {
        var html = LoadFixture("search_results_basic.html");

        var response = _parser.Parse(html);

        response.VqdToken.Should().Be("vqd_token_xyz");
        response.NextPageParams.Should().Be("nextparams_token_abc");
        response.PreviousPageParams.Should().BeNull();
    }

    [Fact]
    public void Parse_BasicResults_HasNoInstantAnswer()
    {
        var html = LoadFixture("search_results_basic.html");

        var response = _parser.Parse(html);

        response.InstantAnswer.Should().BeNull();
    }

    [Fact]
    public void Parse_WithInstantAnswer_ExtractsInstantAnswer()
    {
        var html = LoadFixture("search_results_with_instant_answer.html");

        var response = _parser.Parse(html);

        response.InstantAnswer.Should().NotBeNull();
        response.InstantAnswer!.Text.Should().Contain("free and open-source software framework");
        response.InstantAnswer.Url.Should().Be("https://en.wikipedia.org/wiki/.NET_Framework");
    }

    [Fact]
    public void Parse_WithInstantAnswer_AlsoHasSearchResults()
    {
        var html = LoadFixture("search_results_with_instant_answer.html");

        var response = _parser.Parse(html);

        response.Results.Should().HaveCount(1);
        response.Results[0].Title.Should().Contain(".NET Framework");
    }

    [Fact]
    public void Parse_EmptyResults_ReturnsEmptyList()
    {
        var html = LoadFixture("search_results_empty.html");

        var response = _parser.Parse(html);

        response.Results.Should().BeEmpty();
        response.InstantAnswer.Should().BeNull();
    }

    [Fact]
    public void Parse_WithPagination_ExtractsBothNavParams()
    {
        var html = LoadFixture("search_results_with_pagination.html");

        var response = _parser.Parse(html);

        response.PreviousPageParams.Should().Be("prev_params_token");
        response.NextPageParams.Should().Be("next_params_token");
        response.VqdToken.Should().Be("vqd_page2_token");
    }

    [Fact]
    public void Parse_WithStartIndex_OffsetsResultIndices()
    {
        var html = LoadFixture("search_results_basic.html");

        var response = _parser.Parse(html, startIndex: 10);

        response.Results[0].Index.Should().Be(11);
        response.Results[4].Index.Should().Be(15);
    }

    [Fact]
    public void Parse_EdgeCases_SkipsEmptyUrlResults()
    {
        var html = LoadFixture("search_results_edge_cases.html");

        var response = _parser.Parse(html);

        // Should have 3 results (the 4th has empty URL and should be skipped)
        response.Results.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_EdgeCases_HandlesDirectUrls()
    {
        var html = LoadFixture("search_results_edge_cases.html");

        var response = _parser.Parse(html);

        response.Results[0].Url.Should().Be("https://example.com/direct-url");
        response.Results[0].Title.Should().Be("Direct URL Result");
    }

    [Fact]
    public void Parse_EdgeCases_HandlesUrlsWithQueryParams()
    {
        var html = LoadFixture("search_results_edge_cases.html");

        var response = _parser.Parse(html);

        response.Results[1].Url.Should().Be("https://example.com/path?query=value&other=1");
    }

    [Fact]
    public void Parse_EdgeCases_HandlesUrlsWithEncodedSpaces()
    {
        var html = LoadFixture("search_results_edge_cases.html");

        var response = _parser.Parse(html);

        response.Results[2].Url.Should().Be("https://example.com/path/with%20spaces");
    }
}
