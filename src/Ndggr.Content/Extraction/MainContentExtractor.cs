using SmartReader;

namespace Ndggr.Content.Extraction;

/// <summary>
/// Extracts main article content from HTML using SmartReader (Readability algorithm).
/// </summary>
internal static class MainContentExtractor
{
    /// <summary>
    /// Extract main content from HTML. Returns the article HTML, text content, and metadata.
    /// </summary>
    public static ExtractionResult Extract(string html, string url)
    {
        var reader = new Reader(url, html);

        var article = reader.GetArticle();

        if (!article.IsReadable)
        {
            return new ExtractionResult
            {
                Html = html,
                TextContent = StripTags(html),
                IsReadable = false
            };
        }

        return new ExtractionResult
        {
            Html = article.Content ?? "",
            TextContent = article.TextContent ?? "",
            Title = article.Title,
            Author = article.Author,
            PublishedDate = article.PublicationDate?.ToString("yyyy-MM-dd"),
            Language = article.Language,
            SiteName = article.SiteName,
            Excerpt = article.Excerpt,
            IsReadable = true
        };
    }

    private static string StripTags(string html)
    {
        // Simple tag stripping fallback for non-readable content
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();
    }
}

internal sealed record ExtractionResult
{
    public required string Html { get; init; }
    public required string TextContent { get; init; }
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? PublishedDate { get; init; }
    public string? Language { get; init; }
    public string? SiteName { get; init; }
    public string? Excerpt { get; init; }
    public bool IsReadable { get; init; }
}
