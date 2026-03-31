using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExplorer.Content;
using WebExplorer.Content.Models;
using WebExplorer.Cli.Output;

namespace WebExplorer.Cli.Commands;

internal static class FetchCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonCompactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Command Create()
    {
        var urlArgument = new Argument<string>("url")
        {
            Description = "URL to fetch and extract content from"
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Output format: markdown, json, jsonl (default: markdown)",
            DefaultValueFactory = _ => "markdown"
        };

        var chunkSizeOption = new Option<int>("--chunk-size")
        {
            Description = "Target size per chunk in characters (0 = no chunking)",
            DefaultValueFactory = _ => 0
        };

        var maxChunksOption = new Option<int>("--max-chunks")
        {
            Description = "Maximum number of chunks (0 = unlimited)",
            DefaultValueFactory = _ => 0
        };

        var includeLinksOption = new Option<bool>("--include-links")
        {
            Description = "Include extracted links in output"
        };

        var noMainContentOption = new Option<bool>("--no-main-content")
        {
            Description = "Skip main content extraction (return full HTML as markdown)"
        };

        var proxyOption = new Option<string?>("-p", "--proxy")
        {
            Description = "HTTPS proxy URI"
        };

        var timeoutOption = new Option<int>("--timeout-ms")
        {
            Description = "HTTP timeout in milliseconds",
            DefaultValueFactory = _ => 30_000
        };

        var retriesOption = new Option<int>("--max-retries")
        {
            Description = "Number of retries for transient errors",
            DefaultValueFactory = _ => 2
        };

        var prettyOption = new Option<bool>("--pretty")
        {
            Description = "Pretty-print JSON output"
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Write output to file instead of stdout"
        };

        var command = new Command("fetch", "Fetch a URL and extract content as markdown or JSON")
        {
            urlArgument,
            formatOption,
            chunkSizeOption,
            maxChunksOption,
            includeLinksOption,
            noMainContentOption,
            proxyOption,
            timeoutOption,
            retriesOption,
            prettyOption,
            outputOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var url = parseResult.GetValue(urlArgument)!;
            var format = parseResult.GetValue(formatOption)!;
            var chunkSize = parseResult.GetValue(chunkSizeOption);
            var maxChunks = parseResult.GetValue(maxChunksOption);
            var includeLinks = parseResult.GetValue(includeLinksOption);
            var noMainContent = parseResult.GetValue(noMainContentOption);
            var proxyStr = parseResult.GetValue(proxyOption);
            var timeoutMs = parseResult.GetValue(timeoutOption);
            var maxRetries = parseResult.GetValue(retriesOption);
            var pretty = parseResult.GetValue(prettyOption);
            var outputPath = parseResult.GetValue(outputOption);

            var proxy = ProxyResolver.Resolve(proxyStr);

            var options = new ContentExtractionOptions
            {
                MainContentOnly = !noMainContent,
                IncludeLinks = includeLinks,
                ChunkSize = chunkSize,
                MaxChunks = maxChunks,
                TimeoutMs = timeoutMs,
                MaxRetries = maxRetries,
                Proxy = proxy
            };

            try
            {
                using var pipeline = new ContentPipeline(options);
                var doc = await pipeline.ProcessAsync(url, options, cancellationToken);

                var output = FormatOutput(doc, format, pretty);

                if (outputPath is not null)
                {
                    await File.WriteAllTextAsync(outputPath, output, cancellationToken);
                }
                else
                {
                    Console.Write(output);
                }
            }
            catch (WebExplorerException ex)
            {
                ResultFormatter.WriteError(ex.Message);
            }
            catch (Exception ex)
            {
                ResultFormatter.WriteError($"Fetch failed: {ex.Message}");
            }
        });

        return command;
    }

    private static string FormatOutput(ContentDocument doc, string format, bool pretty)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Serialize(doc, pretty ? JsonOptions : JsonCompactOptions),
            "jsonl" => JsonSerializer.Serialize(doc, JsonCompactOptions),
            _ => doc.Markdown ?? ""
        };
    }
}
