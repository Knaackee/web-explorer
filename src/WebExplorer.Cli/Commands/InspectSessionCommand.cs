using System.CommandLine;
using WebExplorer.Cli.Output;
using WebExplorer.Playwright;

namespace WebExplorer.Cli.Commands;

internal static class InspectSessionCommand
{
    public static Command Create()
    {
        var sessionArgument = new Argument<string>("session-id")
        {
            Description = "Session ID to inspect"
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output the session as JSON"
        };

        var command = new Command("inspect-session", "Inspect a Playwright browser session")
        {
            sessionArgument,
            jsonOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var sessionId = parseResult.GetValue(sessionArgument)!;
            var manager = new PlaywrightSessionManager();
            var session = await manager.GetSessionAsync(sessionId, cancellationToken);
            if (session is null)
                throw new InvalidOperationException($"Session '{sessionId}' does not exist.");

            SessionFormatter.WriteSession(session, parseResult.GetValue(jsonOption));
        });

        return command;
    }
}
