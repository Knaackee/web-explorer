using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace WebExplorer.Content.Markdown;

/// <summary>
/// Converts HTML to Markdown using an embedded html-to-markdown Go binary
/// with DOM-level pre-cleaning via AngleSharp.
/// </summary>
internal static partial class HtmlToMarkdownConverter
{
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var cleanedHtml = CleanHtml(html);

        var binaryPath = NativeBinaryManager.BinaryPath;

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = "--plugin-table --plugin-strikethrough",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8
        };

        process.Start();

        // Write stdin, read stdout/stderr all concurrently to avoid deadlocks
        var stdinTask = Task.Run(() =>
        {
            process.StandardInput.Write(cleanedHtml);
            process.StandardInput.Close();
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        Task.WaitAll(stdinTask, stdoutTask, stderrTask);
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new ContentExtractionException(
                $"html2markdown conversion failed (exit code {process.ExitCode}): {stderrTask.Result}");

        var markdown = stdoutTask.Result;

        // Collapse excessive blank lines (3+ newlines → 2)
        markdown = ExcessiveNewlinesRegex().Replace(markdown, "\n\n");

        return markdown.Trim();
    }

    /// <summary>
    /// DOM-level cleaning before markdown conversion.
    /// Strips universally unwanted elements — content-level noise stripping
    /// (nav/footer) is handled by HtmlPreProcessor before SmartReader.
    /// </summary>
    private static string CleanHtml(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var body = document.Body;
        if (body == null)
            return html;

        foreach (var el in body.QuerySelectorAll("svg").ToArray())
            el.Remove();

        foreach (var el in body.QuerySelectorAll("[hidden], [aria-hidden='true']").ToArray())
            el.Remove();

        foreach (var img in body.QuerySelectorAll("img").ToArray())
        {
            var src = img.GetAttribute("src") ?? "";
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(src))
            {
                img.Remove();
            }
        }

        foreach (var a in body.QuerySelectorAll("a").ToArray())
        {
            var href = a.GetAttribute("href") ?? "";

            if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                a.ReplaceWith(document.CreateTextNode(a.TextContent));
                continue;
            }

            if (string.IsNullOrWhiteSpace(a.TextContent) && a.QuerySelector("img") == null)
                a.Remove();
        }

        var result = body.InnerHtml;
        result = HtmlCommentRegex().Replace(result, "");
        return result;
    }

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlinesRegex();
}
