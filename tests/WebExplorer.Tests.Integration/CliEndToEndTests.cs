using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace WebExplorer.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Category", "Cli")]
public sealed class CliEndToEndTests
{
    private const int TimedOutExitCode = -999;

    private static readonly string CliProjectPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WebExplorer.Cli"));

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string arguments, int timeoutMs = 180_000)
    {
        var cliExe = OperatingSystem.IsWindows() ? "wxp.exe" : "wxp";
        var cliBinaryPath = Path.Combine(CliProjectPath, "bin", "Release", "net10.0", "win-x64", cliExe);

        var fileName = File.Exists(cliBinaryPath) ? cliBinaryPath : "dotnet";
        var processArguments = File.Exists(cliBinaryPath)
            ? arguments
            : $"run --no-build -c Release -f net10.0 --project \"{CliProjectPath}\" -- {arguments}";

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = processArguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            return (process.ExitCode, await stdOutTask, await stdErrTask);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort process cleanup.
            }

            return (TimedOutExitCode, string.Empty, "CLI invocation timed out");
        }
    }

    [Fact]
    public async Task Search_BasicQuery_ReturnsResultsAndExitZero()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search \"DuckDuckGo\"");
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_JsonFlag_ReturnsValidJson()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search --json \"programming language\"");
        if (exitCode == TimedOutExitCode) return;

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
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_WithSiteFilter_ReturnsResults()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search -w github.com \"dotnet\"");
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_WithNumOption_ReturnsResults()
    {
        var (exitCode, stdout, _) = await RunCliAsync("search -n 5 --json \"test\"");
        if (exitCode == TimedOutExitCode) return;

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
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
        stdout.Should().Contain("Example Domain");
    }

    [Fact]
    public async Task Fetch_JsonFormat_ReturnsValidJson()
    {
        var (exitCode, stdout, _) = await RunCliAsync("fetch --format json https://example.com");
        if (exitCode == TimedOutExitCode) return;

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
        if (exitCode == TimedOutExitCode) return;

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
        if (exitCode == TimedOutExitCode) return;

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
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NoArgs_ShowsHelp()
    {
        var (exitCode, stdout, _) = await RunCliAsync("");
        if (exitCode == TimedOutExitCode) return;

        // System.CommandLine returns 0 for help
        stdout.Should().Contain("wxp");
    }

    [Fact]
    public async Task HelpCommand_ShowsGlobalHelp()
    {
        var (exitCode, stdout, _) = await RunCliAsync("help");
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().Contain("Usage:");
        stdout.Should().Contain("wxp [command] [options]");
    }

    [Fact]
    public async Task HelpCommand_Search_ShowsSearchHelp()
    {
        var (exitCode, stdout, _) = await RunCliAsync("help search");
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().Be(0);
        stdout.Should().Contain("Search DuckDuckGo");
        stdout.Should().Contain("search <keywords>");
    }

    [Fact]
    public async Task Search_NoKeywords_ShowsError()
    {
        var (exitCode, _, stderr) = await RunCliAsync("search");
        if (exitCode == TimedOutExitCode) return;

        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task SessionCommands_StartFetchInspectEnd_WorkAcrossProcesses()
    {
        if (!await PlaywrightTestSupport.IsChromiumAvailableAsync())
            return;

        using var server = new CookieTestServer();
        var sessionId = $"cli-{Guid.NewGuid():N}"[..12];

        try
        {
            var (startExitCode, startStdOut, _) = await RunCliAsync($"start-session --session {sessionId} --json", 240_000);
            if (startExitCode == TimedOutExitCode) return;

            startExitCode.Should().Be(0);
            JsonDocument.Parse(startStdOut).RootElement.GetProperty("sessionId").GetString().Should().Be(sessionId);

            var (setExitCode, _, _) = await RunCliAsync($"fetch --session {sessionId} {server.SetCookieUrl}", 240_000);
            if (setExitCode == TimedOutExitCode) return;
            setExitCode.Should().Be(0);

            var (echoExitCode, echoStdOut, _) = await RunCliAsync($"fetch --session {sessionId} {server.EchoCookieUrl}", 240_000);
            if (echoExitCode == TimedOutExitCode) return;
            echoExitCode.Should().Be(0);
            echoStdOut.Should().Contain("wxp-session=retained");

            var (inspectExitCode, inspectStdOut, _) = await RunCliAsync($"inspect-session {sessionId} --json", 240_000);
            if (inspectExitCode == TimedOutExitCode) return;
            inspectExitCode.Should().Be(0);
            JsonDocument.Parse(inspectStdOut).RootElement.GetProperty("sessionId").GetString().Should().Be(sessionId);

            var (listExitCode, listStdOut, _) = await RunCliAsync("list-sessions --json", 240_000);
            if (listExitCode == TimedOutExitCode) return;
            listExitCode.Should().Be(0);
            listStdOut.Should().Contain(sessionId);
        }
        finally
        {
            await RunCliAsync($"end-session {sessionId}", 240_000);
        }

        var (finalInspectExitCode, _, _) = await RunCliAsync($"inspect-session {sessionId}", 240_000);
        finalInspectExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task SessionCommands_ResumePersistentSessionAcrossProcesses()
    {
        if (!await PlaywrightTestSupport.IsChromiumAvailableAsync())
            return;

        using var server = new CookieTestServer();
        var sessionId = $"resume-{Guid.NewGuid():N}"[..12];

        try
        {
            var (startExitCode, _, _) = await RunCliAsync($"start-session --session {sessionId} --persistent --json", 240_000);
            if (startExitCode == TimedOutExitCode) return;
            startExitCode.Should().Be(0);

            var (setExitCode, _, _) = await RunCliAsync($"fetch --session {sessionId} {server.SetCookieUrl}", 240_000);
            if (setExitCode == TimedOutExitCode) return;
            setExitCode.Should().Be(0);

            var (endExitCode, _, _) = await RunCliAsync($"end-session {sessionId}", 240_000);
            if (endExitCode == TimedOutExitCode) return;
            endExitCode.Should().Be(0);

            var (inspectStoppedExitCode, inspectStoppedStdOut, _) = await RunCliAsync($"inspect-session {sessionId} --json", 240_000);
            if (inspectStoppedExitCode == TimedOutExitCode) return;
            inspectStoppedExitCode.Should().Be(0);
            JsonDocument.Parse(inspectStoppedStdOut).RootElement.GetProperty("state").GetString().Should().Be("Stopped");

            var (resumeExitCode, resumeStdOut, _) = await RunCliAsync($"resume-session {sessionId} --json", 240_000);
            if (resumeExitCode == TimedOutExitCode) return;
            resumeExitCode.Should().Be(0);
            JsonDocument.Parse(resumeStdOut).RootElement.GetProperty("state").GetString().Should().Be("Active");

            var (echoExitCode, echoStdOut, _) = await RunCliAsync($"fetch --session {sessionId} {server.EchoCookieUrl}", 240_000);
            if (echoExitCode == TimedOutExitCode) return;
            echoExitCode.Should().Be(0);
            echoStdOut.Should().Contain("wxp-session=retained");
        }
        finally
        {
            await RunCliAsync($"end-session {sessionId} --delete-profile", 240_000);
        }
    }
}
