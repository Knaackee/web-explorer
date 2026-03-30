using ReverseMarkdown;

namespace Ndggr.Content.Markdown;

/// <summary>
/// Converts HTML to Markdown using ReverseMarkdown.
/// </summary>
internal static class HtmlToMarkdownConverter
{
    private static readonly Converter Converter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true
    });

    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var markdown = Converter.Convert(html);

        // Clean up excessive blank lines
        while (markdown.Contains("\n\n\n", StringComparison.Ordinal))
        {
            markdown = markdown.Replace("\n\n\n", "\n\n");
        }

        return markdown.Trim();
    }
}
