using System.CommandLine;
using WebExplorer.Cli.Commands;

var rootCommand = new RootCommand("wxp - DuckDuckGo search from the terminal");

rootCommand.Subcommands.Add(SearchCommand.Create());
rootCommand.Subcommands.Add(FetchCommand.Create());
rootCommand.Subcommands.Add(StartSessionCommand.Create());
rootCommand.Subcommands.Add(ResumeSessionCommand.Create());
rootCommand.Subcommands.Add(EndSessionCommand.Create());
rootCommand.Subcommands.Add(ListSessionsCommand.Create());
rootCommand.Subcommands.Add(InspectSessionCommand.Create());
rootCommand.Subcommands.Add(HelpCommand.Create(rootCommand));

return rootCommand.Parse(args).Invoke();
