using Ndggr.Content.Chunking;
using Ndggr.Content.Extraction;
using Ndggr.Content.Markdown;
using Ndggr.Content.Models;

namespace Ndggr.Content;

/// <summary>
/// High-level pipeline: URL → fetch → extract → markdown → chunk → ContentDocument.
/// </summary>
public sealed class ContentPipeline : IDisposable
{
    private readonly ContentFetchClient _fetchClient;
    private readonly bool _ownsFetchClient;

    public ContentPipeline(ContentExtractionOptions? options = null)
    {
        _fetchClient = new ContentFetchClient(options);
        _ownsFetchClient = true;
    }

    internal ContentPipeline(ContentFetchClient fetchClient)
    {
        _fetchClient = fetchClient;
        _ownsFetchClient = false;
    }

    /// <summary>
    /// Fetch a URL and extract structured content.
    /// </summary>
    public async Task<ContentDocument> ProcessAsync(string url, ContentExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        options ??= new ContentExtractionOptions();

        string html;
        try
        {
            html = await _fetchClient.FetchHtmlAsync(url, options, cancellationToken).ConfigureAwait(false);
        }
        catch (NdggrException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ContentFetchException($"Failed to fetch {url}: {ex.Message}", ex);
        }

        return ExtractFromHtml(html, url, options);
    }

    /// <summary>
    /// Extract structured content from already-fetched HTML.
    /// </summary>
    public ContentDocument ExtractFromHtml(string html, string url, ContentExtractionOptions? options = null)
    {
        options ??= new ContentExtractionOptions();

        try
        {
            var extraction = options.MainContentOnly
                ? MainContentExtractor.Extract(html, url)
                : new ExtractionResult
                {
                    Html = html,
                    TextContent = "",
                    IsReadable = false
                };

            var markdown = HtmlToMarkdownConverter.Convert(extraction.Html);
            var textContent = extraction.TextContent;
            var wordCount = CountWords(textContent);

            IReadOnlyList<ContentChunk>? chunks = null;
            if (options.ChunkSize > 0)
            {
                chunks = HeadingAwareChunker.Chunk(markdown, url, options.ChunkSize, options.MaxChunks);
            }

            IReadOnlyList<ExtractedLink>? links = null;
            if (options.IncludeLinks)
            {
                links = LinkExtractor.Extract(extraction.Html, url);
            }

            return new ContentDocument
            {
                SchemaVersion = options.SchemaVersion,
                Url = url,
                Title = extraction.Title,
                Author = extraction.Author,
                PublishedDate = extraction.PublishedDate,
                Language = extraction.Language,
                SiteName = extraction.SiteName,
                Excerpt = extraction.Excerpt,
                Markdown = markdown,
                TextContent = textContent,
                WordCount = wordCount,
                Chunks = chunks,
                Links = links,
                FetchedAt = DateTimeOffset.UtcNow
            };
        }
        catch (NdggrException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ContentExtractionException($"Failed to extract content from {url}: {ex.Message}", ex);
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    public void Dispose()
    {
        if (_ownsFetchClient)
            _fetchClient.Dispose();
    }
}
