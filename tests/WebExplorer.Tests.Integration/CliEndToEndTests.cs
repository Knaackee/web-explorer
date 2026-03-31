using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace WebExplorer.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class CliEndToEndTests
{
    private static readonly string CliProjectPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WebExplorer.Cli"));

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string arguments, int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{CliProjectPath}\" -- {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        using var cts = new CancellationTokenSource(timeoutMs);

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }

    [Fact]
    public async Task Search_BasicQuery_ReturnsResultsAndExitZero()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search \"DuckDuckGo\"");

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_JsonFlag_ReturnsValidJson()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search --json \"programming language\"");

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();

        // Might contain "No results." or "Error:" due to DDG rate limiting
        if (stdout.TrimStart().StartsWith('['))
        {
            var jsonDoc = JsonDocument.Parse(stdout);
            jsonDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
            if (jsonDoc.RootElement.GetArrayLength() > 0)
            {
                var first = jsonDoc.RootElement[0];
                first.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
                first.GetProperty("url").GetString().Should().NotBeNullOrWhiteSpace();
                first.GetProperty("abstract").GetString().Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    [Fact]
    public async Task Search_WithRegion_ReturnsResults()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search -r de-de \"Berlin\"");

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_WithSiteFilter_ReturnsResults()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search -w github.com \"dotnet\"");

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_WithNumOption_ReturnsResults()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search -n 5 --json \"test\"");

        exitCode.Should().Be(0);

        // If DDG returned results, verify the JSON structure
        if (stdout.TrimStart().StartsWith('['))
        {
            var jsonDoc = JsonDocument.Parse(stdout);
            jsonDoc.RootElement.GetArrayLength().Should().BeLessThanOrEqualTo(10);
        }
    }

    [Fact]
    public async Task Fetch_ExampleDotCom_ReturnsMarkdown()
    {
        var (exitCode, stdout, _) = await RunCliAsync("fetch https://example.com");

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
        stdout.Should().Contain("Example Domain");
    }

    [Fact]
    public async Task Fetch_JsonFormat_ReturnsValidJson()
    {
        var (exitCode, stdout, _) = await RunCliAsync("fetch --format json https://example.com");

        exitCode.Should().Be(0);

        var jsonDoc = JsonDocument.Parse(stdout);
        jsonDoc.RootElement.GetProperty("url").GetString().Should().Contain("example.com");
        jsonDoc.RootElement.GetProperty("markdown").GetString().Should().NotBeNullOrWhiteSpace();
        jsonDoc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Fetch_WithChunking_ReturnsChunks()
    {
        var (exitCode, stdout, _) = await RunCliAsync(
            "fetch --format json --chunk-size 200 https://example.com");

        exitCode.Should().Be(0);

        var jsonDoc = JsonDocument.Parse(stdout);
        jsonDoc.RootElement.TryGetProperty("chunks", out var chunks).Should().BeTrue();
        chunks.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Fetch_WithIncludeLinks_ReturnsLinks()
    {
        var (exitCode, stdout, _) = await RunCliAsync(
            "fetch --format json --include-links https://example.com");

        exitCode.Should().Be(0);

        var jsonDoc = JsonDocument.Parse(stdout);
        jsonDoc.RootElement.TryGetProperty("links", out var links).Should().BeTrue();
        links.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Fetch_NoMainContent_ReturnsFullContent()
    {
        var (exitCode, stdout, _) = await RunCliAsync(
            "fetch --no-main-content https://example.com");

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NoArgs_ShowsHelp()
    {
        var (exitCode, stdout, _) = await RunCliAsync("");

        // System.CommandLine returns 0 for help
        stdout.Should().Contain("wxp");
    }

    [Fact]
    public async Task HelpCommand_ShowsGlobalHelp()
    {
        var (exitCode, stdout, _) = await RunCliAsync("help");

        exitCode.Should().Be(0);
        stdout.Should().Contain("Usage:");
        stdout.Should().Contain("wxp [command] [options]");
    }

    [Fact]
    public async Task HelpCommand_Search_ShowsSearchHelp()
    {
        var (exitCode, stdout, _) = await RunCliAsync("help search");

        exitCode.Should().Be(0);
        stdout.Should().Contain("Search DuckDuckGo");
        stdout.Should().Contain("search <keywords>");
    }

    [Fact]
    public async Task Search_NoKeywords_ShowsError()
    {
        var (exitCode, _, stderr) = await RunCliAsync("search");

        exitCode.Should().NotBe(0);
    }
}
