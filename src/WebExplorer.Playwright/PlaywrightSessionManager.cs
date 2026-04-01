using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace WebExplorer.Playwright;

public enum PlaywrightSessionState
{
    Active,
    Stopped,
    Broken,
    Expired
}

public enum PlaywrightWaitUntil
{
    Load,
    DomContentLoaded,
    NetworkIdle
}

public sealed record PlaywrightSessionStartOptions
{
    public string? SessionId { get; init; }
    public bool Persistent { get; init; }
    public bool Headless { get; init; } = true;
    public string? UserDataDir { get; init; }
    public Uri? Proxy { get; init; }
    public string? UserAgent { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public int MaxConcurrentFetches { get; init; } = 1;
    public int IdleTimeoutSeconds { get; init; } = 3600;
}

public sealed record PlaywrightNavigationOptions
{
    public int TimeoutMs { get; init; } = 30_000;
    public PlaywrightWaitUntil WaitUntil { get; init; } = PlaywrightWaitUntil.NetworkIdle;
    public int ConcurrencyWaitTimeoutMs { get; init; } = 30_000;
}

public sealed record PlaywrightSessionInfo
{
    public required string SessionId { get; init; }
    public required PlaywrightSessionState State { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastAccessedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string UserDataDir { get; init; }
    public required int ProcessId { get; init; }
    public required int DebugPort { get; init; }
    public required bool Persistent { get; init; }
    public required bool Headless { get; init; }
    public required int MaxConcurrentFetches { get; init; }
    public Uri? Proxy { get; init; }
}

public interface IPlaywrightSessionManager
{
    Task<PlaywrightSessionInfo> StartSessionAsync(
        PlaywrightSessionStartOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<PlaywrightSessionInfo> ResumeSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task EndSessionAsync(string sessionId, bool deleteSessionData = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaywrightSessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default);

    Task<PlaywrightSessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<string> FetchHtmlAsync(
        string sessionId,
        string url,
        PlaywrightNavigationOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class PlaywrightSessionException : Exception
{
    public PlaywrightSessionException(string message)
        : base(message)
    {
    }

    public PlaywrightSessionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class PlaywrightSessionManager : IPlaywrightSessionManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<PlaywrightSessionInfo> StartSessionAsync(
        PlaywrightSessionStartOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PlaywrightSessionStartOptions();
        ValidateStartOptions(options);

        var sessionId = options.SessionId ?? CreateSessionId();
        ValidateSessionId(sessionId);

        await CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);

        var sessionDirectory = PlaywrightStoragePaths.GetSessionDirectory(sessionId);
        var metadataPath = PlaywrightStoragePaths.GetSessionMetadataPath(sessionId);

        if (File.Exists(metadataPath))
        {
            var existing = await LoadMetadataAsync(sessionDirectory, cancellationToken).ConfigureAwait(false);
            if (existing?.Persistent == true)
                throw new PlaywrightSessionException($"Session '{sessionId}' already exists. Use resume-session to reopen it.");

            throw new PlaywrightSessionException($"Session '{sessionId}' already exists.");
        }

        Directory.CreateDirectory(sessionDirectory);

        var userDataDirectory = options.UserDataDir ?? Path.Combine(sessionDirectory, "profile");
        Directory.CreateDirectory(userDataDirectory);

        var now = DateTimeOffset.UtcNow;
        var metadata = await StartBrowserSessionAsync(new PlaywrightSessionMetadata
        {
            SessionId = sessionId,
            State = PlaywrightSessionState.Active,
            CreatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = now.AddSeconds(options.IdleTimeoutSeconds),
            UserDataDir = Path.GetFullPath(userDataDirectory),
            ProcessId = 0,
            DebugPort = 0,
            Persistent = options.Persistent,
            Headless = options.Headless,
            Proxy = options.Proxy?.AbsoluteUri,
            UserAgent = options.UserAgent,
            Headers = new Dictionary<string, string>(options.Headers),
            MaxConcurrentFetches = options.MaxConcurrentFetches,
            IdleTimeoutSeconds = options.IdleTimeoutSeconds
        }, cancellationToken).ConfigureAwait(false);

        await SaveMetadataAsync(metadata, cancellationToken).ConfigureAwait(false);

        return metadata.ToSessionInfo();
    }

    public async Task<PlaywrightSessionInfo> ResumeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);

        var sessionDirectory = PlaywrightStoragePaths.GetSessionDirectory(sessionId);
        var metadata = await LoadMetadataAsync(sessionDirectory, cancellationToken).ConfigureAwait(false)
            ?? throw new PlaywrightSessionException($"Session '{sessionId}' does not exist.");

        if (!metadata.Persistent)
            throw new PlaywrightSessionException($"Session '{sessionId}' is not persistent and cannot be resumed.");

        metadata = await RefreshStateAsync(metadata, cancellationToken).ConfigureAwait(false);
        if (metadata.State == PlaywrightSessionState.Active)
            return metadata.ToSessionInfo();

        if (!Directory.Exists(metadata.UserDataDir))
            throw new PlaywrightSessionException($"Session '{sessionId}' cannot be resumed because its browser profile directory is missing.");

        var resumed = await StartBrowserSessionAsync(metadata with
        {
            State = PlaywrightSessionState.Active,
            ProcessId = 0,
            DebugPort = 0,
        }, cancellationToken).ConfigureAwait(false);

        await RestoreSessionStateAsync(resumed, cancellationToken).ConfigureAwait(false);

        await SaveMetadataAsync(resumed, cancellationToken).ConfigureAwait(false);
        return resumed.ToSessionInfo();
    }

    public async Task EndSessionAsync(string sessionId, bool deleteSessionData = false, CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);

        var sessionDirectory = PlaywrightStoragePaths.GetSessionDirectory(sessionId);
        var metadata = await LoadMetadataAsync(sessionDirectory, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
            return;

        if (metadata.ProcessId > 0)
        {
            if (metadata.Persistent)
            {
                await SaveSessionStateAsync(metadata, cancellationToken).ConfigureAwait(false);
                await PlaywrightBrowserUtilities.TryCloseBrowserGracefullyAsync(
                    metadata.DebugPort,
                    metadata.ProcessId,
                    cancellationToken).ConfigureAwait(false);
            }

            if (PlaywrightBrowserUtilities.IsProcessAlive(metadata.ProcessId))
                PlaywrightBrowserUtilities.TryKillProcess(metadata.ProcessId);
        }

        if (!metadata.Persistent || deleteSessionData)
        {
            PlaywrightBrowserUtilities.TryDeleteDirectory(sessionDirectory);
            return;
        }

        var stopped = metadata with
        {
            State = PlaywrightSessionState.Stopped,
            ProcessId = 0,
            DebugPort = 0,
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.MaxValue,
        };

        await SaveMetadataAsync(stopped, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PlaywrightSessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        await CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);

        var sessionsRoot = PlaywrightStoragePaths.GetRootDirectory();
        if (!Directory.Exists(sessionsRoot))
            return [];

        var results = new List<PlaywrightSessionInfo>();
        foreach (var sessionDirectory in Directory.GetDirectories(sessionsRoot))
        {
            if (string.Equals(Path.GetFileName(sessionDirectory), "shared-browser", StringComparison.OrdinalIgnoreCase))
                continue;

            var metadata = await LoadMetadataAsync(sessionDirectory, cancellationToken).ConfigureAwait(false);
            if (metadata is null)
                continue;

            metadata = await RefreshStateAsync(metadata, cancellationToken).ConfigureAwait(false);
            results.Add(metadata.ToSessionInfo());
        }

        return results
            .OrderBy(session => session.SessionId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<PlaywrightSessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);

        await CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);

        var metadata = await LoadMetadataAsync(PlaywrightStoragePaths.GetSessionDirectory(sessionId), cancellationToken).ConfigureAwait(false);
        if (metadata is null)
            return null;

        metadata = await RefreshStateAsync(metadata, cancellationToken).ConfigureAwait(false);
        return metadata.ToSessionInfo();
    }

    public async Task<string> FetchHtmlAsync(
        string sessionId,
        string url,
        PlaywrightNavigationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        options ??= new PlaywrightNavigationOptions();

        var sessionDirectory = PlaywrightStoragePaths.GetSessionDirectory(sessionId);
        var metadata = await LoadMetadataAsync(sessionDirectory, cancellationToken).ConfigureAwait(false)
            ?? throw new PlaywrightSessionException($"Session '{sessionId}' does not exist.");

        metadata = await RefreshStateAsync(metadata, cancellationToken).ConfigureAwait(false);
        if (metadata.State == PlaywrightSessionState.Expired)
            throw new PlaywrightSessionException($"Session '{sessionId}' has expired.");
        if (metadata.State == PlaywrightSessionState.Stopped)
            throw new PlaywrightSessionException($"Session '{sessionId}' is stopped. Use resume-session before fetching.");
        if (metadata.State == PlaywrightSessionState.Broken)
            throw new PlaywrightSessionException($"Session '{sessionId}' is broken and must be resumed or restarted.");

        await using var lease = await PlaywrightConcurrencyLease.AcquireAsync(
            sessionDirectory,
            metadata.MaxConcurrentFetches,
            options.ConcurrencyWaitTimeoutMs,
            cancellationToken).ConfigureAwait(false);

        await using var connection = await PlaywrightBrowserUtilities.ConnectToBrowserAsync(metadata.DebugPort, cancellationToken).ConfigureAwait(false);
        var context = PlaywrightBrowserUtilities.GetDefaultContext(connection.Browser);
        if (metadata.Headers.Count > 0)
            await context.SetExtraHTTPHeadersAsync(metadata.Headers).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        try
        {
            page.SetDefaultNavigationTimeout(options.TimeoutMs);

            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = options.TimeoutMs,
                WaitUntil = PlaywrightBrowserUtilities.MapWaitUntil(options.WaitUntil)
            }).ConfigureAwait(false);

            var html = await page.ContentAsync().ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            var updatedMetadata = metadata with
            {
                State = PlaywrightSessionState.Active,
                LastAccessedAt = now,
                ExpiresAt = now.AddSeconds(metadata.IdleTimeoutSeconds)
            };

            await SaveMetadataAsync(updatedMetadata, cancellationToken).ConfigureAwait(false);
            return html;
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            var refreshedState = PlaywrightBrowserUtilities.IsProcessAlive(metadata.ProcessId)
                ? metadata.State
                : PlaywrightSessionState.Broken;

            await SaveMetadataAsync(metadata with { State = refreshedState }, cancellationToken).ConfigureAwait(false);
            throw new PlaywrightSessionException($"Failed to fetch '{url}' using session '{sessionId}': {ex.Message}", ex);
        }
        finally
        {
            try
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static void ValidateStartOptions(PlaywrightSessionStartOptions options)
    {
        if (options.MaxConcurrentFetches <= 0)
            throw new PlaywrightSessionException("MaxConcurrentFetches must be greater than zero.");
        if (options.IdleTimeoutSeconds <= 0)
            throw new PlaywrightSessionException("IdleTimeoutSeconds must be greater than zero.");
        if (options.UserDataDir is not null && string.IsNullOrWhiteSpace(options.UserDataDir))
            throw new PlaywrightSessionException("UserDataDir must not be empty when specified.");
    }

    private static void ValidateSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new PlaywrightSessionException("Session ID must not be empty.");

        foreach (var character in sessionId)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
                continue;

            throw new PlaywrightSessionException("Session IDs may only contain letters, digits, '-' and '_'.");
        }
    }

    private static string CreateSessionId() => $"pw-{Guid.NewGuid():N}"[..15];

    private static async Task<PlaywrightSessionMetadata> StartBrowserSessionAsync(
        PlaywrightSessionMetadata metadata,
        CancellationToken cancellationToken)
    {
        var executablePath = await PlaywrightBrowserUtilities.GetExecutablePathAsync(cancellationToken).ConfigureAwait(false);
        var debugPort = PlaywrightBrowserUtilities.GetFreeTcpPort();
        var process = PlaywrightBrowserUtilities.LaunchBrowserProcess(
            executablePath,
            debugPort,
            metadata.UserDataDir,
            metadata.Headless,
            metadata.Proxy is null ? null : new Uri(metadata.Proxy),
            metadata.UserAgent);

        try
        {
            await PlaywrightBrowserUtilities.WaitForDebugEndpointAsync(debugPort, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            PlaywrightBrowserUtilities.TryKillProcess(process.Id);
            throw;
        }

        var now = DateTimeOffset.UtcNow;
        var started = metadata with
        {
            State = PlaywrightSessionState.Active,
            LastAccessedAt = now,
            ExpiresAt = now.AddSeconds(metadata.IdleTimeoutSeconds),
            ProcessId = process.Id,
            DebugPort = debugPort,
        };

        if (started.Headers.Count > 0)
        {
            await using var connection = await PlaywrightBrowserUtilities.ConnectToBrowserAsync(started.DebugPort, cancellationToken).ConfigureAwait(false);
            var context = PlaywrightBrowserUtilities.GetDefaultContext(connection.Browser);
            await context.SetExtraHTTPHeadersAsync(started.Headers).ConfigureAwait(false);
        }

        return started;
    }

    private static async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var sessionsRoot = PlaywrightStoragePaths.GetRootDirectory();
        if (!Directory.Exists(sessionsRoot))
            return;

        foreach (var sessionDirectory in Directory.GetDirectories(sessionsRoot))
        {
            if (string.Equals(Path.GetFileName(sessionDirectory), "shared-browser", StringComparison.OrdinalIgnoreCase))
                continue;

            var metadata = await LoadMetadataAsync(sessionDirectory, cancellationToken).ConfigureAwait(false);
            if (metadata is null)
                continue;

            if (DateTimeOffset.UtcNow <= metadata.ExpiresAt)
                continue;

            if (metadata.ProcessId > 0)
                PlaywrightBrowserUtilities.TryKillProcess(metadata.ProcessId);

            if (!metadata.Persistent)
            {
                PlaywrightBrowserUtilities.TryDeleteDirectory(sessionDirectory);
                continue;
            }

            var stoppedMetadata = metadata with
            {
                State = PlaywrightSessionState.Stopped,
                ProcessId = 0,
                DebugPort = 0,
                ExpiresAt = DateTimeOffset.MaxValue,
            };

            await SaveMetadataAsync(stoppedMetadata, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<PlaywrightSessionMetadata> RefreshStateAsync(
        PlaywrightSessionMetadata metadata,
        CancellationToken cancellationToken)
    {
        var refreshed = metadata;
        if (metadata.State == PlaywrightSessionState.Stopped)
        {
            return metadata;
        }

        if (DateTimeOffset.UtcNow > metadata.ExpiresAt)
        {
            if (metadata.ProcessId > 0)
                PlaywrightBrowserUtilities.TryKillProcess(metadata.ProcessId);

            refreshed = metadata.Persistent
                ? metadata with
                {
                    State = PlaywrightSessionState.Stopped,
                    ProcessId = 0,
                    DebugPort = 0,
                    ExpiresAt = DateTimeOffset.MaxValue,
                }
                : metadata with { State = PlaywrightSessionState.Expired };
        }
        else if (metadata.ProcessId > 0 && !PlaywrightBrowserUtilities.IsProcessAlive(metadata.ProcessId))
        {
            refreshed = metadata with { State = PlaywrightSessionState.Broken };
        }

        if (!Equals(refreshed, metadata))
            await SaveMetadataAsync(refreshed, cancellationToken).ConfigureAwait(false);

        return refreshed;
    }

    private static async Task<PlaywrightSessionMetadata?> LoadMetadataAsync(string sessionDirectory, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(sessionDirectory, "session.json");
        if (!File.Exists(metadataPath))
            return null;

        await using var stream = File.OpenRead(metadataPath);
        return await JsonSerializer.DeserializeAsync<PlaywrightSessionMetadata>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveMetadataAsync(PlaywrightSessionMetadata metadata, CancellationToken cancellationToken)
    {
        var sessionDirectory = PlaywrightStoragePaths.GetSessionDirectory(metadata.SessionId);
        Directory.CreateDirectory(sessionDirectory);

        var metadataPath = PlaywrightStoragePaths.GetSessionMetadataPath(metadata.SessionId);
        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, metadata, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveSessionStateAsync(
        PlaywrightSessionMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (metadata.DebugPort <= 0)
            return;

        try
        {
            await using var connection = await PlaywrightBrowserUtilities.ConnectToBrowserAsync(metadata.DebugPort, cancellationToken).ConfigureAwait(false);
            var context = PlaywrightBrowserUtilities.GetDefaultContext(connection.Browser);
            await context.StorageStateAsync(new BrowserContextStorageStateOptions
            {
                Path = PlaywrightStoragePaths.GetSessionStorageStatePath(metadata.SessionId)
            }).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort persistence of session state.
        }
    }

    private static async Task RestoreSessionStateAsync(
        PlaywrightSessionMetadata metadata,
        CancellationToken cancellationToken)
    {
        var storageStatePath = PlaywrightStoragePaths.GetSessionStorageStatePath(metadata.SessionId);
        if (metadata.DebugPort <= 0 || !File.Exists(storageStatePath))
            return;

        try
        {
            var storageState = await LoadStorageStateAsync(storageStatePath, cancellationToken).ConfigureAwait(false);
            if (storageState is null)
                return;

            await using var connection = await PlaywrightBrowserUtilities.ConnectToBrowserAsync(metadata.DebugPort, cancellationToken).ConfigureAwait(false);
            var context = PlaywrightBrowserUtilities.GetDefaultContext(connection.Browser);

            if (storageState.Cookies.Count > 0)
            {
                var restoredCookies = storageState.Cookies
                    .Select(cookie => new Cookie
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Url = cookie.Url,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        Expires = cookie.Expires is { } expires && expires >= 0 ? expires : null,
                        HttpOnly = cookie.HttpOnly,
                        Secure = cookie.Secure,
                        SameSite = cookie.SameSite,
                        PartitionKey = cookie.PartitionKey
                    })
                    .ToArray();

                await context.AddCookiesAsync(restoredCookies).ConfigureAwait(false);
            }

            foreach (var origin in storageState.Origins)
            {
                if (origin.LocalStorage.Count == 0)
                    continue;

                var page = await context.NewPageAsync().ConfigureAwait(false);
                try
                {
                    await page.GotoAsync(origin.Origin, new PageGotoOptions
                    {
                        Timeout = 15_000,
                        WaitUntil = WaitUntilState.Load
                    }).ConfigureAwait(false);

                    var entries = origin.LocalStorage
                        .Select(entry => new Dictionary<string, string>
                        {
                            ["name"] = entry.Name,
                            ["value"] = entry.Value
                        })
                        .ToArray();

                    await page.EvaluateAsync(
                        "entries => { for (const entry of entries) { localStorage.setItem(entry.name, entry.value); } }",
                        entries).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort restore for origin-scoped storage.
                }
                finally
                {
                    try
                    {
                        await page.CloseAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }
                }
            }
        }
        catch
        {
            // Best-effort restore of session state.
        }
    }

    private static async Task<PlaywrightStorageState?> LoadStorageStateAsync(string storageStatePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(storageStatePath);
        return await JsonSerializer.DeserializeAsync<PlaywrightStorageState>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed record PlaywrightSessionMetadata
    {
        public required string SessionId { get; init; }
        public required PlaywrightSessionState State { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset LastAccessedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public required string UserDataDir { get; init; }
        public required int ProcessId { get; init; }
        public required int DebugPort { get; init; }
        public required bool Persistent { get; init; }
        public required bool Headless { get; init; }
        public required int MaxConcurrentFetches { get; init; }
        public required int IdleTimeoutSeconds { get; init; }
        public string? Proxy { get; init; }
        public string? UserAgent { get; init; }
        public Dictionary<string, string> Headers { get; init; } = [];

        public PlaywrightSessionInfo ToSessionInfo() => new()
        {
            SessionId = SessionId,
            State = State,
            CreatedAt = CreatedAt,
            LastAccessedAt = LastAccessedAt,
            ExpiresAt = ExpiresAt,
            UserDataDir = UserDataDir,
            ProcessId = ProcessId,
            DebugPort = DebugPort,
            Persistent = Persistent,
            Headless = Headless,
            MaxConcurrentFetches = MaxConcurrentFetches,
            Proxy = Proxy is null ? null : new Uri(Proxy)
        };
    }

    private sealed record PlaywrightStorageState
    {
        public List<Cookie> Cookies { get; init; } = [];
        public List<StorageOrigin> Origins { get; init; } = [];
    }

    private sealed record StorageOrigin
    {
        public required string Origin { get; init; }
        public List<StorageEntry> LocalStorage { get; init; } = [];
    }

    private sealed record StorageEntry
    {
        public required string Name { get; init; }
        public required string Value { get; init; }
    }
}
