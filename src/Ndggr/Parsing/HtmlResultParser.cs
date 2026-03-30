using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Ndggr.Parsing;

/// <summary>
/// Parses DuckDuckGo HTML search result pages into structured <see cref="SearchResult"/> objects.
/// </summary>
public sealed class HtmlResultParser
{
    private static readonly HtmlParser Parser = new();

    /// <summary>
    /// Parse a DDG HTML response into a <see cref="DdgSearchResponse"/>.
    /// </summary>
    public DdgSearchResponse Parse(string html, int startIndex = 0)
    {
        var document = Parser.ParseDocument(html);

        var results = ParseResults(document, startIndex);
        var instantAnswer = ParseInstantAnswer(document);
        var (vqd, nextParams, prevParams) = ParseNavigationParams(document);

        return new DdgSearchResponse
        {
            Results = results,
            InstantAnswer = instantAnswer,
            VqdToken = vqd,
            NextPageParams = nextParams,
            PreviousPageParams = prevParams
        };
    }

    private static List<SearchResult> ParseResults(IHtmlDocument document, int startIndex)
    {
        var results = new List<SearchResult>();
        var resultDivs = document.QuerySelectorAll("div.links_main");
        var index = startIndex;

        foreach (var div in resultDivs)
        {
            var titleElement = div.QuerySelector("h2.result__title a");
            if (titleElement is null)
                continue;

            var rawUrl = titleElement.GetAttribute("href") ?? "";
            var url = ExtractActualUrl(rawUrl);

            if (string.IsNullOrWhiteSpace(url))
                continue;

            var title = CleanText(titleElement.TextContent);
            var snippetElement = div.QuerySelector("a.result__snippet");
            var snippet = snippetElement is not null ? CleanText(snippetElement.TextContent) : "";

            index++;
            results.Add(new SearchResult
            {
                Index = index,
                Title = title,
                Url = url,
                Snippet = snippet
            });
        }

        return results;
    }

    private static InstantAnswer? ParseInstantAnswer(IHtmlDocument document)
    {
        var zciResult = document.QuerySelector("div.zci__result");
        if (zciResult is null)
            return null;

        var text = CleanText(zciResult.TextContent);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var link = zciResult.QuerySelector("a");
        var url = link?.GetAttribute("href");

        return new InstantAnswer
        {
            Text = text,
            Url = url
        };
    }

    private static (string? Vqd, string? NextParams, string? PrevParams) ParseNavigationParams(IHtmlDocument document)
    {
        string? vqd = null;
        string? nextParams = null;
        string? prevParams = null;

        // DDG puts navigation data in <input> elements within nav-link divs
        var navLinks = document.QuerySelectorAll("div.nav-link");
        var npButtons = new List<string>();

        foreach (var navDiv in navLinks)
        {
            var npInput = navDiv.QuerySelector("input[name='nextParams']");
            if (npInput is not null)
            {
                var val = npInput.GetAttribute("value");
                if (!string.IsNullOrEmpty(val))
                    npButtons.Add(val);
            }

            var vqdInput = navDiv.QuerySelector("input[name='vqd']");
            if (vqdInput is not null)
            {
                var val = vqdInput.GetAttribute("value");
                if (!string.IsNullOrEmpty(val))
                    vqd = val;
            }
        }

        // First button is previous (if page > 0), last button is next
        if (npButtons.Count == 1)
        {
            nextParams = npButtons[0];
        }
        else if (npButtons.Count >= 2)
        {
            prevParams = npButtons[0];
            nextParams = npButtons[^1];
        }

        return (vqd, nextParams, prevParams);
    }

    /// <summary>
    /// Extract the actual URL from a DDG redirect link.
    /// DDG wraps URLs like: //duckduckgo.com/l/?uddg=ENCODED_URL&amp;rut=...
    /// </summary>
    internal static string ExtractActualUrl(string ddgUrl)
    {
        if (string.IsNullOrWhiteSpace(ddgUrl))
            return "";

        // If it's a DDG redirect URL, extract the uddg parameter
        if (ddgUrl.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
        {
            var uri = ddgUrl.StartsWith("//", StringComparison.Ordinal)
                ? new Uri("https:" + ddgUrl)
                : new Uri(ddgUrl, UriKind.RelativeOrAbsolute);

            if (uri.IsAbsoluteUri)
            {
                var query = uri.Query;
                var uddgStart = query.IndexOf("uddg=", StringComparison.OrdinalIgnoreCase);
                if (uddgStart >= 0)
                {
                    uddgStart += 5; // length of "uddg="
                    var uddgEnd = query.IndexOf('&', uddgStart);
                    var encoded = uddgEnd >= 0 ? query[uddgStart..uddgEnd] : query[uddgStart..];
                    return Uri.UnescapeDataString(encoded);
                }
            }
        }

        // Direct URL (no redirect wrapper)
        if (ddgUrl.StartsWith("//", StringComparison.Ordinal))
            return "https:" + ddgUrl;

        return ddgUrl;
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Normalize whitespace: collapse runs of whitespace into single spaces
        var span = text.AsSpan().Trim();
        return CollapseWhitespace(span);
    }

    private static string CollapseWhitespace(ReadOnlySpan<char> input)
    {
        var buffer = new char[input.Length];
        var pos = 0;
        var prevWasSpace = false;

        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!prevWasSpace)
                {
                    buffer[pos++] = ' ';
                    prevWasSpace = true;
                }
            }
            else
            {
                buffer[pos++] = c;
                prevWasSpace = false;
            }
        }

        return new string(buffer, 0, pos);
    }
}
