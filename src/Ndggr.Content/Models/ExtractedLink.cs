using System.Text.Json.Serialization;

namespace Ndggr.Content.Models;

/// <summary>
/// A link extracted from the document.
/// </summary>
public sealed record ExtractedLink
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("href")]
    public required string Href { get; init; }
}
