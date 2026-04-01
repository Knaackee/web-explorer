using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using WebExplorer.Playwright;

namespace WebExplorer.Cli.Output;

internal static class SessionFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void WriteSession(PlaywrightSessionInfo session, bool asJson)
    {
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(session, JsonOptions));
            return;
        }

        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(session.SessionId)}[/]");
        AnsiConsole.MarkupLine($"  state: {Markup.Escape(session.State.ToString())}");
        AnsiConsole.MarkupLine($"  pid: {session.ProcessId}");
        AnsiConsole.MarkupLine($"  port: {session.DebugPort}");
        AnsiConsole.MarkupLine($"  persistent: {session.Persistent}");
        AnsiConsole.MarkupLine($"  headless: {session.Headless}");
        AnsiConsole.MarkupLine($"  lastAccessedAt: {Markup.Escape(session.LastAccessedAt.ToString("O"))}");
        AnsiConsole.MarkupLine($"  expiresAt: {Markup.Escape(session.ExpiresAt.ToString("O"))}");
    }

    public static void WriteSessions(IReadOnlyList<PlaywrightSessionInfo> sessions, bool asJson)
    {
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(sessions, JsonOptions));
            return;
        }

        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No sessions.[/]");
            return;
        }

        foreach (var session in sessions)
        {
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(session.SessionId)}[/] [dim]({session.State})[/]");
        }
    }
}
