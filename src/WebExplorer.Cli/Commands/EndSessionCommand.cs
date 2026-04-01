using System.CommandLine;
using Spectre.Console;
using WebExplorer.Playwright;

namespace WebExplorer.Cli.Commands;

internal static class EndSessionCommand
{
    public static Command Create()
    {
        var sessionArgument = new Argument<string>("session-id")
        {
            Description = "Session ID to stop"
        };

        var deleteProfileOption = new Option<bool>("--delete-profile")
        {
            Description = "Delete session metadata and browser profile instead of keeping a resumable persistent session"
        };

        var command = new Command("end-session", "End a Playwright-backed browser session")
        {
            sessionArgument,
            deleteProfileOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var sessionId = parseResult.GetValue(sessionArgument)!;
            var manager = new PlaywrightSessionManager();
            await manager.EndSessionAsync(sessionId, parseResult.GetValue(deleteProfileOption), cancellationToken);
            AnsiConsole.MarkupLine($"[green]Ended session[/] {Markup.Escape(sessionId)}");
        });

        return command;
    }
}
