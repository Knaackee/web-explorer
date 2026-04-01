using System.CommandLine;

namespace WebExplorer.Cli.Commands;

internal static class HelpCommand
{
    public static Command Create(RootCommand rootCommand)
    {
        var topicArgument = new Argument<string[]>("topic")
        {
            Description = "Optional command to show help for (e.g. 'search', 'fetch' or 'start-session')",
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("help", "Show help for wxp or a subcommand")
        {
            topicArgument
        };

        command.SetAction(parseResult =>
        {
            var topic = parseResult.GetValue(topicArgument) ?? [];
            var args = topic.Length == 0
                ? new[] { "--help" }
                : topic.Concat(new[] { "--help" }).ToArray();

            return rootCommand.Parse(args).Invoke();
        });

        return command;
    }
}
