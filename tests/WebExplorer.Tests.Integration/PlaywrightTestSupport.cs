using Microsoft.Playwright;
using WebExplorer.Playwright;

namespace WebExplorer.Tests.Integration;

internal static class PlaywrightTestSupport
{
    private static bool? _chromiumAvailable;

    public static async Task<bool> IsChromiumAvailableAsync()
    {
        if (_chromiumAvailable.HasValue)
            return _chromiumAvailable.Value;

        try
        {
            await PlaywrightRuntimeInstaller.EnsureChromiumInstalledAsync();
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            _chromiumAvailable = !string.IsNullOrWhiteSpace(playwright.Chromium.ExecutablePath)
                && File.Exists(playwright.Chromium.ExecutablePath);
        }
        catch
        {
            _chromiumAvailable = false;
        }

        return _chromiumAvailable.Value;
    }
}
