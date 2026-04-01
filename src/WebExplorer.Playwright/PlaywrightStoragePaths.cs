namespace WebExplorer.Playwright;

internal static class PlaywrightStoragePaths
{
    public static string GetRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.GetTempPath()
            : localAppData;

        return Path.Combine(baseDirectory, "web-explorer", "playwright-sessions");
    }

    public static string GetSessionDirectory(string sessionId)
        => Path.Combine(GetRootDirectory(), sessionId);

    public static string GetSessionMetadataPath(string sessionId)
        => Path.Combine(GetSessionDirectory(sessionId), "session.json");

    public static string GetSessionStorageStatePath(string sessionId)
        => Path.Combine(GetSessionDirectory(sessionId), "storage-state.json");

    public static string GetSharedBrowserDirectory(string configHash)
        => Path.Combine(GetRootDirectory(), "shared-browser", configHash);

    public static string GetSharedBrowserMetadataPath(string configHash)
        => Path.Combine(GetSharedBrowserDirectory(configHash), "browser.json");

    public static string GetInstallLockPath()
        => Path.Combine(GetRootDirectory(), ".install.lock");
}
