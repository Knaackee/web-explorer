using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace Ndggr.Cli.Output;

internal static class ResultFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void WriteConsole(DdgSearchResponse response, bool expandUrls)
    {
        if (response.InstantAnswer is not null)
        {
            AnsiConsole.MarkupLine($"  [bold yellow]{Markup.Escape(response.InstantAnswer.Text)}[/]");
            AnsiConsole.WriteLine();
        }

        if (response.Results.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No results.[/]");
            return;
        }

        foreach (var result in response.Results)
        {
            WriteResult(result, expandUrls);
        }
    }

    private static void WriteResult(SearchResult result, bool expandUrls)
    {
        var indexStr = result.Index.ToString().PadLeft(3);

        if (expandUrls)
        {
            AnsiConsole.MarkupLine($" [green]{indexStr}.[/] [bold]{Markup.Escape(result.Title)}[/]");
            AnsiConsole.MarkupLine($"      [blue underline]{Markup.Escape(result.Url)}[/]");
        }
        else
        {
            var host = GetHost(result.Url);
            AnsiConsole.MarkupLine($" [green]{indexStr}.[/] [bold]{Markup.Escape(result.Title)}[/] [blue]\\[{Markup.Escape(host)}][/]");
        }

        if (!string.IsNullOrWhiteSpace(result.Snippet))
        {
            AnsiConsole.MarkupLine($"      [dim]{Markup.Escape(result.Snippet)}[/]");
        }

        AnsiConsole.WriteLine();
    }

    public static void WriteJson(DdgSearchResponse response, TextWriter writer)
    {
        var output = response.Results.Select(r => new
        {
            r.Title,
            r.Url,
            Abstract = r.Snippet
        });

        writer.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
    }

    public static void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red bold]Error:[/] {Markup.Escape(message)}");
    }

    private static string GetHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host;
        return url;
    }
}
