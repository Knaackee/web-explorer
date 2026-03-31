using System.Text.Json.Serialization;

namespace WebExplorer.Content.Models;

/// <summary>
/// A heading-aware chunk of document content.
/// </summary>
public sealed record ContentChunk
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("heading")]
    public string? Heading { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("charCount")]
    public int CharCount { get; init; }
}
