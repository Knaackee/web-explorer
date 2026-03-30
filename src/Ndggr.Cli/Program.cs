using System.CommandLine;
using Ndggr.Cli.Commands;

var rootCommand = new RootCommand("ndggr — DuckDuckGo search from the terminal");

rootCommand.Subcommands.Add(SearchCommand.Create());

return rootCommand.Parse(args).Invoke();
