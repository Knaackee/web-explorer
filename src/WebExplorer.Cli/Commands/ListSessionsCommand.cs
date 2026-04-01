using System.CommandLine;
using WebExplorer.Cli.Output;
using WebExplorer.Playwright;

namespace WebExplorer.Cli.Commands;

internal static class ListSessionsCommand
{
    public static Command Create()
    {
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output sessions as JSON"
        };

        var command = new Command("list-sessions", "List known Playwright browser sessions")
        {
            jsonOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var manager = new PlaywrightSessionManager();
            var sessions = await manager.ListSessionsAsync(cancellationToken);
            SessionFormatter.WriteSessions(sessions, parseResult.GetValue(jsonOption));
        });

        return command;
    }
}
