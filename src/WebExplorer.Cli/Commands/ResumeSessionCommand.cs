using System.CommandLine;
using WebExplorer.Cli.Output;
using WebExplorer.Playwright;

namespace WebExplorer.Cli.Commands;

internal static class ResumeSessionCommand
{
    public static Command Create()
    {
        var sessionArgument = new Argument<string>("session-id")
        {
            Description = "Persistent session ID to resume"
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output the resumed session as JSON"
        };

        var command = new Command("resume-session", "Resume a persistent Playwright browser session")
        {
            sessionArgument,
            jsonOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var manager = new PlaywrightSessionManager();
            var session = await manager.ResumeSessionAsync(parseResult.GetValue(sessionArgument)!, cancellationToken);
            SessionFormatter.WriteSession(session, parseResult.GetValue(jsonOption));
        });

        return command;
    }
}
