using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace WebExplorer.Playwright;

internal static class PlaywrightBrowserUtilities
{
    private static readonly HttpClient EndpointHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    public static async Task<string> GetExecutablePathAsync(CancellationToken cancellationToken = default)
    {
        await PlaywrightRuntimeInstaller.EnsureChromiumInstalledAsync(cancellationToken).ConfigureAwait(false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
        var executablePath = playwright.Chromium.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            throw new PlaywrightSessionException("Playwright Chromium is not available after installation.");

        return executablePath;
    }

    public static Process LaunchBrowserProcess(
        string executablePath,
        int debugPort,
        string userDataDirectory,
        bool headless,
        Uri? proxy,
        string? userAgent)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = headless,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        psi.ArgumentList.Add($"--remote-debugging-port={debugPort}");
        psi.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
        psi.ArgumentList.Add($"--user-data-dir={userDataDirectory}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add("--disable-sync");
        psi.ArgumentList.Add("--disable-features=Translate,OptimizationHints,MediaRouter");

        // GitHub-hosted Linux runners need a less restrictive Chromium startup profile.
        if (OperatingSystem.IsLinux())
        {
            psi.ArgumentList.Add("--no-sandbox");
            psi.ArgumentList.Add("--disable-dev-shm-usage");
        }

        if (headless)
            psi.ArgumentList.Add("--headless=new");

        if (proxy is not null)
            psi.ArgumentList.Add($"--proxy-server={proxy.AbsoluteUri}");

        if (!string.IsNullOrWhiteSpace(userAgent))
            psi.ArgumentList.Add($"--user-agent={userAgent}");

        psi.ArgumentList.Add("about:blank");

        var process = Process.Start(psi)
            ?? throw new PlaywrightSessionException("Failed to start the Playwright browser process.");

        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        return process;
    }

    public static async Task WaitForDebugEndpointAsync(int debugPort, CancellationToken cancellationToken)
    {
        var endpoint = $"http://127.0.0.1:{debugPort}/json/version";
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await EndpointHttpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch
            {
                // Browser is still starting.
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        throw new PlaywrightSessionException("Timed out while waiting for the Playwright browser session to start.");
    }

    public static async Task<BrowserConnection> ConnectToBrowserAsync(int debugPort, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
        try
        {
            var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{debugPort}").ConfigureAwait(false);
            return new BrowserConnection(playwright, browser);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    public static async Task TryCloseBrowserGracefullyAsync(
        int debugPort,
        int processId,
        CancellationToken cancellationToken)
    {
        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        shutdownCts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await using var connection = await ConnectToBrowserAsync(debugPort, shutdownCts.Token).ConfigureAwait(false);
            var closeTask = connection.Browser.CloseAsync();
            await Task.WhenAny(closeTask, Task.Delay(TimeSpan.FromSeconds(5), shutdownCts.Token)).ConfigureAwait(false);
        }
        catch
        {
            // Fall back to process termination below.
        }

        if (processId <= 0)
            return;

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
                return;

            await process.WaitForExitAsync(shutdownCts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort wait; hard kill is handled by the caller when needed.
        }
    }

    public static IBrowserContext GetDefaultContext(IBrowser browser)
        => browser.Contexts.FirstOrDefault()
            ?? throw new PlaywrightSessionException("The browser session has no default context.");

    public static WaitUntilState MapWaitUntil(PlaywrightWaitUntil waitUntil) => waitUntil switch
    {
        PlaywrightWaitUntil.Load => WaitUntilState.Load,
        PlaywrightWaitUntil.DomContentLoaded => WaitUntilState.DOMContentLoaded,
        _ => WaitUntilState.NetworkIdle
    };

    public static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public static void TryKillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    public static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    internal sealed class BrowserConnection(IPlaywright playwright, IBrowser browser) : IAsyncDisposable
    {
        public IBrowser Browser { get; } = browser;

        public async ValueTask DisposeAsync()
        {
            await Browser.DisposeAsync().ConfigureAwait(false);
            playwright.Dispose();
        }
    }
}
