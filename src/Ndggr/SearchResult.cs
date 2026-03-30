namespace Ndggr;

/// <summary>
/// A single DuckDuckGo search result.
/// </summary>
public sealed record SearchResult
{
    public required int Index { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Snippet { get; init; }
}
