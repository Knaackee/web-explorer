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
            AnsiConsole.MarkupLine($" [green]{indexStr}.[/] [bold]{Markup.Escape(result.Title)}[/] [blue][[{Markup.Escape(result.Url)}]][/]");
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

    public static void WriteInstantAnswer(DdgSearchResponse response)
    {
        if (response.InstantAnswer is not null)
        {
            AnsiConsole.MarkupLine($"  [bold yellow]{Markup.Escape(response.InstantAnswer.Text)}[/]");
            if (response.InstantAnswer.Url is not null)
            {
                AnsiConsole.MarkupLine($"  [blue underline]{Markup.Escape(response.InstantAnswer.Url)}[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No instant answer available.[/]");
        }
    }

}
