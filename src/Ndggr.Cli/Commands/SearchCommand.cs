using System.CommandLine;
using Ndggr.Cli.Output;

namespace Ndggr.Cli.Commands;

internal static class SearchCommand
{
    public static Command Create()
    {
        var keywordsArgument = new Argument<string[]>("keywords")
        {
            Description = "Search keywords",
            Arity = ArgumentArity.OneOrMore
        };

        var numOption = new Option<int>("-n", "--num")
        {
            Description = "Results per page (0–25, default 10)",
            DefaultValueFactory = _ => 10
        };

        var regionOption = new Option<string>("-r", "--region")
        {
            Description = "Region (e.g. de-de, us-en)",
            DefaultValueFactory = _ => "us-en"
        };

        var timeOption = new Option<string?>("-t", "--time")
        {
            Description = "Time filter: d (day), w (week), m (month), y (year)"
        };

        var siteOption = new Option<string?>("-w", "--site")
        {
            Description = "Site-specific search"
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output results as JSON"
        };

        var expandOption = new Option<bool>("-x", "--expand")
        {
            Description = "Show full URLs"
        };

        var command = new Command("search", "Search DuckDuckGo")
        {
            keywordsArgument,
            numOption,
            regionOption,
            timeOption,
            siteOption,
            jsonOption,
            expandOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var keywords = parseResult.GetValue(keywordsArgument)!;
            var num = parseResult.GetValue(numOption);
            var region = parseResult.GetValue(regionOption)!;
            var time = parseResult.GetValue(timeOption);
            var site = parseResult.GetValue(siteOption);
            var json = parseResult.GetValue(jsonOption);
            var expand = parseResult.GetValue(expandOption);

            var query = string.Join(' ', keywords);
            var options = new DdgSearchOptions
            {
                NumResults = num,
                Region = region,
                TimeFilter = time ?? "",
                Site = site
            };

            try
            {
                using var client = new DdgClient(options);
                var response = await client.SearchAsync(query, options, cancellationToken);

                if (json)
                {
                    ResultFormatter.WriteJson(response, Console.Out);
                }
                else
                {
                    ResultFormatter.WriteConsole(response, expand);
                }
            }
            catch (HttpRequestException ex)
            {
                ResultFormatter.WriteError($"Search failed: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                ResultFormatter.WriteError("Search timed out.");
            }
        });

        return command;
    }
}
