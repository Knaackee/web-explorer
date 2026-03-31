using AngleSharp;
using AngleSharp.Html.Parser;
using WebExplorer.Content.Models;

namespace WebExplorer.Content.Extraction;

/// <summary>
/// Extracts links from HTML content.
/// </summary>
internal static class LinkExtractor
{
    public static IReadOnlyList<ExtractedLink> Extract(string html, string baseUrl)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;
        var document = parser.ParseDocument(html);

        var links = new List<ExtractedLink>();

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href") ?? "";
            var text = anchor.TextContent.Trim();

            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Resolve relative URLs
            if (Uri.TryCreate(new Uri(baseUrl), href, out var resolved))
            {
                href = resolved.AbsoluteUri;
            }

            if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(href))
            {
                links.Add(new ExtractedLink
                {
                    Text = text,
                    Href = href
                });
            }
        }

        return links;
    }
}
