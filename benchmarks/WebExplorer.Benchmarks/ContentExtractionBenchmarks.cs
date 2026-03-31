using BenchmarkDotNet.Attributes;
using WebExplorer.Content;
using WebExplorer.Content.Chunking;
using WebExplorer.Content.Extraction;
using WebExplorer.Content.Markdown;

namespace WebExplorer.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class ContentExtractionBenchmarks
{
    private string _articleHtml = null!;
    private string _newsHtml = null!;
    private string _wikiHtml = null!;
    private string _extractedMarkdown = null!;
    private ContentPipeline _pipeline = null!;

    [GlobalSetup]
    public void Setup()
    {
        _articleHtml = File.ReadAllText(Path.Combine("Fixtures", "content_article.html"));
        _newsHtml = File.ReadAllText(Path.Combine("Fixtures", "golden_news_article.html"));
        _wikiHtml = File.ReadAllText(Path.Combine("Fixtures", "golden_wiki_article.html"));
        _pipeline = new ContentPipeline();

        // Pre-extract markdown for chunking benchmarks
        var doc = _pipeline.ExtractFromHtml(_articleHtml, "https://example.com/article");
        _extractedMarkdown = doc.Markdown ?? "";
    }

    [GlobalCleanup]
    public void Cleanup() => _pipeline.Dispose();

    [Benchmark(Description = "Readability extraction (article)")]
    public object ExtractArticle() =>
        MainContentExtractor.Extract(_articleHtml, "https://example.com/article");

    [Benchmark(Description = "Readability extraction (news)")]
    public object ExtractNews() =>
        MainContentExtractor.Extract(_newsHtml, "https://example.com/news");

    [Benchmark(Description = "Readability extraction (wiki)")]
    public object ExtractWiki() =>
        MainContentExtractor.Extract(_wikiHtml, "https://example.com/wiki");

    [Benchmark(Description = "HTML → Markdown conversion")]
    public string ConvertToMarkdown() =>
        HtmlToMarkdownConverter.Convert(_articleHtml);

    [Benchmark(Description = "Link extraction (article)")]
    public object ExtractLinks() =>
        LinkExtractor.Extract(_articleHtml, "https://example.com/article");

    [Benchmark(Description = "Chunking (size=500)")]
    public object ChunkMarkdown500() =>
        HeadingAwareChunker.Chunk(_extractedMarkdown, "https://example.com/article", 500);

    [Benchmark(Description = "Chunking (size=200)")]
    public object ChunkMarkdown200() =>
        HeadingAwareChunker.Chunk(_extractedMarkdown, "https://example.com/article", 200);

    [Benchmark(Description = "Full pipeline (article → ContentDocument)")]
    public object FullPipeline() =>
        _pipeline.ExtractFromHtml(_articleHtml, "https://example.com/article",
            new ContentExtractionOptions { ChunkSize = 500, IncludeLinks = true });

    [Benchmark(Description = "Full pipeline (wiki → ContentDocument)")]
    public object FullPipelineWiki() =>
        _pipeline.ExtractFromHtml(_wikiHtml, "https://example.com/wiki",
            new ContentExtractionOptions { ChunkSize = 500, IncludeLinks = true });
}
