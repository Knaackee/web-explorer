using System.CommandLine;
using WebExplorer.Cli.Commands;

var rootCommand = new RootCommand("wxp - DuckDuckGo search from the terminal");

rootCommand.Subcommands.Add(SearchCommand.Create());
rootCommand.Subcommands.Add(FetchCommand.Create());
rootCommand.Subcommands.Add(HelpCommand.Create(rootCommand));

return rootCommand.Parse(args).Invoke();
