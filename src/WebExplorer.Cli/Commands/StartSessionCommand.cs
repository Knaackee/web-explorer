using System.CommandLine;
using WebExplorer.Cli.Output;
using WebExplorer.Playwright;

namespace WebExplorer.Cli.Commands;

internal static class StartSessionCommand
{
    public static Command Create()
    {
        var sessionOption = new Option<string?>("--session")
        {
            Description = "Optional session ID to create"
        };

        var persistentOption = new Option<bool>("--persistent")
        {
            Description = "Keep the browser profile directory after ending the session"
        };

        var headfulOption = new Option<bool>("--headful")
        {
            Description = "Launch the browser with a visible window"
        };

        var idleTimeoutOption = new Option<int>("--idle-timeout-seconds")
        {
            Description = "Session idle timeout in seconds",
            DefaultValueFactory = _ => 3600
        };

        var maxConcurrentFetchesOption = new Option<int>("--max-concurrent-fetches")
        {
            Description = "Maximum simultaneous fetches allowed for this session",
            DefaultValueFactory = _ => 1
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output the session as JSON"
        };

        var command = new Command("start-session", "Start a Playwright-backed browser session")
        {
            sessionOption,
            persistentOption,
            headfulOption,
            idleTimeoutOption,
            maxConcurrentFetchesOption,
            jsonOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var manager = new PlaywrightSessionManager();
            var session = await manager.StartSessionAsync(new PlaywrightSessionStartOptions
            {
                SessionId = parseResult.GetValue(sessionOption),
                Persistent = parseResult.GetValue(persistentOption),
                Headless = !parseResult.GetValue(headfulOption),
                IdleTimeoutSeconds = parseResult.GetValue(idleTimeoutOption),
                MaxConcurrentFetches = parseResult.GetValue(maxConcurrentFetchesOption)
            }, cancellationToken);

            SessionFormatter.WriteSession(session, parseResult.GetValue(jsonOption));
        });

        return command;
    }
}
