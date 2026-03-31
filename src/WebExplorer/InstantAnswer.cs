namespace WebExplorer;

/// <summary>
/// DuckDuckGo Instant Answer result.
/// </summary>
public sealed record InstantAnswer
{
    public required string Text { get; init; }
    public string? Url { get; init; }
}
