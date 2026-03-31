using System.Security.Cryptography;
using System.Text;
using WebExplorer.Content.Models;

namespace WebExplorer.Content.Chunking;

/// <summary>
/// Splits markdown text into heading-aware chunks with stable IDs.
/// </summary>
internal static class HeadingAwareChunker
{
    /// <summary>
    /// Split markdown into chunks. Respects heading boundaries.
    /// </summary>
    public static IReadOnlyList<ContentChunk> Chunk(string markdown, string url, int chunkSize, int maxChunks = 0)
    {
        if (chunkSize <= 0 || string.IsNullOrWhiteSpace(markdown))
            return [];

        var sections = SplitBySections(markdown);
        var chunks = new List<ContentChunk>();
        var index = 0;

        foreach (var section in sections)
        {
            if (maxChunks > 0 && chunks.Count >= maxChunks)
                break;

            if (section.Content.Length <= chunkSize)
            {
                chunks.Add(CreateChunk(section.Heading, section.Content, index++, url));
            }
            else
            {
                // Split large sections by paragraphs
                var paragraphs = section.Content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                var buffer = new StringBuilder();

                foreach (var paragraph in paragraphs)
                {
                    if (maxChunks > 0 && chunks.Count >= maxChunks)
                        break;

                    if (buffer.Length + paragraph.Length + 2 > chunkSize && buffer.Length > 0)
                    {
                        chunks.Add(CreateChunk(section.Heading, buffer.ToString().Trim(), index++, url));
                        buffer.Clear();
                    }

                    if (buffer.Length > 0)
                        buffer.Append("\n\n");
                    buffer.Append(paragraph);
                }

                if (buffer.Length > 0 && (maxChunks == 0 || chunks.Count < maxChunks))
                {
                    chunks.Add(CreateChunk(section.Heading, buffer.ToString().Trim(), index++, url));
                }
            }
        }

        return chunks;
    }

    private static List<Section> SplitBySections(string markdown)
    {
        var lines = markdown.Split('\n');
        var sections = new List<Section>();
        string? currentHeading = null;
        var currentContent = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                if (currentContent.Length > 0)
                {
                    sections.Add(new Section(currentHeading, currentContent.ToString().Trim()));
                    currentContent.Clear();
                }
                currentHeading = line.TrimStart('#').Trim();
            }

            currentContent.AppendLine(line);
        }

        if (currentContent.Length > 0)
        {
            sections.Add(new Section(currentHeading, currentContent.ToString().Trim()));
        }

        return sections;
    }

    private static ContentChunk CreateChunk(string? heading, string content, int index, string url)
    {
        var id = GenerateStableId(url, index);
        return new ContentChunk
        {
            Id = id,
            Index = index,
            Heading = heading,
            Content = content,
            CharCount = content.Length
        };
    }

    private static string GenerateStableId(string url, int index)
    {
        var input = $"{url}#{index}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
#if NET9_0_OR_GREATER
        return System.Convert.ToHexStringLower(hash)[..12];
#else
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..12];
#endif
    }

    private sealed record Section(string? Heading, string Content);
}
