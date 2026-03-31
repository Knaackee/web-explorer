namespace WebExplorer;

/// <summary>
/// Response from a DuckDuckGo search query.
/// </summary>
public sealed record SearchResponse
{
    public IReadOnlyList<SearchResult> Results { get; init; } = [];
    public InstantAnswer? InstantAnswer { get; init; }
    public string? VqdToken { get; init; }
    public string? NextPageParams { get; init; }
    public string? PreviousPageParams { get; init; }
}
