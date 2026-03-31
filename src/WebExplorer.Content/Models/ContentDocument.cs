using System.Text.Json.Serialization;

namespace WebExplorer.Content.Models;

/// <summary>
/// Structured document extracted from a URL, designed for LLM consumption.
/// </summary>
public sealed record ContentDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; init; }

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; init; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; init; }

    [JsonPropertyName("textContent")]
    public string? TextContent { get; init; }

    [JsonPropertyName("wordCount")]
    public int WordCount { get; init; }

    [JsonPropertyName("chunks")]
    public IReadOnlyList<ContentChunk>? Chunks { get; init; }

    [JsonPropertyName("links")]
    public IReadOnlyList<ExtractedLink>? Links { get; init; }

    [JsonPropertyName("fetchedAt")]
    public DateTimeOffset FetchedAt { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
