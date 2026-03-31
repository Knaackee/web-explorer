using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Ndggr.Cli.Output;

#pragma warning disable CS0618 // SendUserAgent is obsolete

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

        var proxyOption = new Option<string?>("-p", "--proxy")
        {
            Description = "HTTPS proxy URI (e.g. http://proxy:8080)"
        };

        var unsafeOption = new Option<bool>("--unsafe")
        {
            Description = "Disable safe search"
        };

        var nouaOption = new Option<bool>("--noua")
        {
            Description = "Disable user agent"
        };

        var instantOption = new Option<bool>("-i", "--instant")
        {
            Description = "Show only instant answer"
        };

        var duckyOption = new Option<bool>("-j", "--ducky")
        {
            Description = "Open first result in browser (I'm Feeling Ducky)"
        };

        var command = new Command("search", "Search DuckDuckGo")
        {
            keywordsArgument,
            numOption,
            regionOption,
            timeOption,
            siteOption,
            jsonOption,
            expandOption,
            proxyOption,
            unsafeOption,
            nouaOption,
            instantOption,
            duckyOption
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
            var proxyStr = parseResult.GetValue(proxyOption);
            var @unsafe = parseResult.GetValue(unsafeOption);
            var noua = parseResult.GetValue(nouaOption);
            var instant = parseResult.GetValue(instantOption);
            var ducky = parseResult.GetValue(duckyOption);

            var proxy = ResolveProxy(proxyStr);

            var query = string.Join(' ', keywords);
            var options = new DdgSearchOptions
            {
                NumResults = num,
                Region = region,
                TimeFilter = time ?? "",
                Site = site,
                SafeSearch = !@unsafe,
                SendUserAgent = !noua,
                Proxy = proxy
            };

            try
            {
                using var client = new DdgClient(options);
                var response = await client.SearchAsync(query, options, cancellationToken);

                if (ducky)
                {
                    HandleDucky(response);
                }
                else if (instant)
                {
                    ResultFormatter.WriteInstantAnswer(response);
                }
                else if (json)
                {
                    ResultFormatter.WriteJson(response, Console.Out);
                }
                else
                {
                    ResultFormatter.WriteConsole(response, expand);
                }
            }
            catch (NdggrException ex)
            {
                ResultFormatter.WriteError(ex.Message);
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

    /// <summary>
    /// Resolve proxy URI with priority: explicit option > HTTPS_PROXY env var.
    /// </summary>
    internal static Uri? ResolveProxy(string? explicitProxy) =>
        ProxyResolver.Resolve(explicitProxy);

    private static void HandleDucky(DdgSearchResponse response)
    {
        if (response.Results.Count == 0)
        {
            ResultFormatter.WriteError("No results found.");
            return;
        }

        var url = response.Results[0].Url;
        OpenUrl(url);
    }

    internal static void OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            Process.Start("xdg-open", url);
        }
    }
}
