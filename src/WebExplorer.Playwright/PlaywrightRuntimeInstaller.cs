using System.Reflection;

namespace WebExplorer.Playwright;

internal static class PlaywrightRuntimeInstaller
{
    private static readonly SemaphoreSlim InProcessGate = new(1, 1);

    public static async Task EnsureChromiumInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (await IsInstalledAsync().ConfigureAwait(false))
            return;

        await InProcessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await IsInstalledAsync().ConfigureAwait(false))
                return;

            Directory.CreateDirectory(PlaywrightStoragePaths.GetRootDirectory());
            await using var lockStream = await AcquireInstallLockAsync(cancellationToken).ConfigureAwait(false);

            if (await IsInstalledAsync().ConfigureAwait(false))
                return;

            var programType = typeof(Microsoft.Playwright.Playwright).Assembly
                .GetType("Microsoft.Playwright.Program", throwOnError: false)
                ?? throw new PlaywrightSessionException("Could not locate the Playwright installer entry point.");

            var mainMethod = programType.GetMethod(
                "Main",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                [typeof(string[])])
                ?? throw new PlaywrightSessionException("Could not locate the Playwright installer method.");

            var result = mainMethod.Invoke(null, [new[] { "install", "chromium" }]);
            if (result is int exitCode && exitCode != 0)
                throw new PlaywrightSessionException($"Playwright Chromium installation failed with exit code {exitCode}.");

            if (!await IsInstalledAsync().ConfigureAwait(false))
                throw new PlaywrightSessionException("Playwright Chromium installation did not produce a usable browser executable.");
        }
        finally
        {
            InProcessGate.Release();
        }
    }

    internal static async Task<bool> IsInstalledAsync()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(playwright.Chromium.ExecutablePath)
            && File.Exists(playwright.Chromium.ExecutablePath);
    }

    private static async Task<FileStream> AcquireInstallLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = PlaywrightStoragePaths.GetInstallLockPath();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true);
            }
            catch (IOException)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
