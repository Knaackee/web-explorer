using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace WebExplorer.Playwright;

public sealed record PlaywrightSharedBrowserOptions
{
    public bool Headless { get; init; } = true;
    public Uri? Proxy { get; init; }
    public string? UserAgent { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public int MaxConcurrentPages { get; init; } = 4;
    public int IdleTimeoutSeconds { get; init; } = 900;
}

public sealed record PlaywrightSharedBrowserInfo
{
    public required string ConfigHash { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastAccessedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required int ProcessId { get; init; }
    public required int DebugPort { get; init; }
    public required bool Headless { get; init; }
    public required int MaxConcurrentPages { get; init; }
    public Uri? Proxy { get; init; }
}

public sealed class PlaywrightSharedBrowserPool
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<string> FetchHtmlAsync(
        string url,
        PlaywrightNavigationOptions? navigationOptions = null,
        PlaywrightSharedBrowserOptions? browserOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        navigationOptions ??= new PlaywrightNavigationOptions();
        browserOptions ??= new PlaywrightSharedBrowserOptions();
        ValidateOptions(browserOptions);

        var browser = await EnsureBrowserAsync(browserOptions, cancellationToken).ConfigureAwait(false);
        var browserDirectory = PlaywrightStoragePaths.GetSharedBrowserDirectory(browser.ConfigHash);

        await using var lease = await PlaywrightConcurrencyLease.AcquireAsync(
            browserDirectory,
            browser.MaxConcurrentPages,
            navigationOptions.ConcurrencyWaitTimeoutMs,
            cancellationToken).ConfigureAwait(false);

        await using var connection = await PlaywrightBrowserUtilities.ConnectToBrowserAsync(browser.DebugPort, cancellationToken).ConfigureAwait(false);
        var context = PlaywrightBrowserUtilities.GetDefaultContext(connection.Browser);
        if (browser.Headers.Count > 0)
            await context.SetExtraHTTPHeadersAsync(browser.Headers).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        try
        {
            page.SetDefaultNavigationTimeout(navigationOptions.TimeoutMs);

            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = navigationOptions.TimeoutMs,
                WaitUntil = PlaywrightBrowserUtilities.MapWaitUntil(navigationOptions.WaitUntil)
            }).ConfigureAwait(false);

            var html = await page.ContentAsync().ConfigureAwait(false);
            await SaveMetadataAsync(browser with
            {
                LastAccessedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(browser.IdleTimeoutSeconds)
            }, cancellationToken).ConfigureAwait(false);

            return html;
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

    public async Task<PlaywrightSharedBrowserInfo?> GetBrowserInfoAsync(
        PlaywrightSharedBrowserOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PlaywrightSharedBrowserOptions();
        var metadata = await LoadMetadataAsync(GetConfigHash(options), cancellationToken).ConfigureAwait(false);
        if (metadata is null)
            return null;

        metadata = await RefreshStateAsync(metadata, cancellationToken).ConfigureAwait(false);
        return metadata.ToInfo();
    }

    public async Task StopBrowserAsync(
        PlaywrightSharedBrowserOptions? options = null,
        bool deleteBrowserData = false,
        CancellationToken cancellationToken = default)
    {
        options ??= new PlaywrightSharedBrowserOptions();
        var configHash = GetConfigHash(options);
        var metadata = await LoadMetadataAsync(configHash, cancellationToken).ConfigureAwait(false);
        if (metadata?.ProcessId > 0)
            PlaywrightBrowserUtilities.TryKillProcess(metadata.ProcessId);

        if (deleteBrowserData)
            PlaywrightBrowserUtilities.TryDeleteDirectory(PlaywrightStoragePaths.GetSharedBrowserDirectory(configHash));

        await DeleteMetadataAsync(configHash, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SharedBrowserMetadata> EnsureBrowserAsync(
        PlaywrightSharedBrowserOptions options,
        CancellationToken cancellationToken)
    {
        var configHash = GetConfigHash(options);
        var metadata = await LoadMetadataAsync(configHash, cancellationToken).ConfigureAwait(false);
        if (metadata is not null)
        {
            metadata = await RefreshStateAsync(metadata, cancellationToken).ConfigureAwait(false);
            if (PlaywrightBrowserUtilities.IsProcessAlive(metadata.ProcessId))
                return metadata;
        }

        var browserDirectory = PlaywrightStoragePaths.GetSharedBrowserDirectory(configHash);
        Directory.CreateDirectory(browserDirectory);
        Directory.CreateDirectory(Path.Combine(browserDirectory, "profile"));

        var executablePath = await PlaywrightBrowserUtilities.GetExecutablePathAsync(cancellationToken).ConfigureAwait(false);
        var debugPort = PlaywrightBrowserUtilities.GetFreeTcpPort();
        var process = PlaywrightBrowserUtilities.LaunchBrowserProcess(
            executablePath,
            debugPort,
            Path.Combine(browserDirectory, "profile"),
            options.Headless,
            options.Proxy,
            options.UserAgent);

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
        var created = new SharedBrowserMetadata
        {
            ConfigHash = configHash,
            CreatedAt = metadata?.CreatedAt ?? now,
            LastAccessedAt = now,
            ExpiresAt = now.AddSeconds(options.IdleTimeoutSeconds),
            ProcessId = process.Id,
            DebugPort = debugPort,
            Headless = options.Headless,
            MaxConcurrentPages = options.MaxConcurrentPages,
            IdleTimeoutSeconds = options.IdleTimeoutSeconds,
            Proxy = options.Proxy?.AbsoluteUri,
            UserAgent = options.UserAgent,
            Headers = new Dictionary<string, string>(options.Headers)
        };

        if (created.Headers.Count > 0)
        {
            await using var connection = await PlaywrightBrowserUtilities.ConnectToBrowserAsync(created.DebugPort, cancellationToken).ConfigureAwait(false);
            var context = PlaywrightBrowserUtilities.GetDefaultContext(connection.Browser);
            await context.SetExtraHTTPHeadersAsync(created.Headers).ConfigureAwait(false);
        }

        await SaveMetadataAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    private static async Task<SharedBrowserMetadata> RefreshStateAsync(
        SharedBrowserMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow <= metadata.ExpiresAt && PlaywrightBrowserUtilities.IsProcessAlive(metadata.ProcessId))
            return metadata;

        if (metadata.ProcessId > 0)
            PlaywrightBrowserUtilities.TryKillProcess(metadata.ProcessId);

        PlaywrightBrowserUtilities.TryDeleteDirectory(PlaywrightStoragePaths.GetSharedBrowserDirectory(metadata.ConfigHash));
        await DeleteMetadataAsync(metadata.ConfigHash, cancellationToken).ConfigureAwait(false);
        return metadata;
    }

    private static async Task<SharedBrowserMetadata?> LoadMetadataAsync(string configHash, CancellationToken cancellationToken)
    {
        var path = PlaywrightStoragePaths.GetSharedBrowserMetadataPath(configHash);
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SharedBrowserMetadata>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveMetadataAsync(SharedBrowserMetadata metadata, CancellationToken cancellationToken)
    {
        var path = PlaywrightStoragePaths.GetSharedBrowserMetadataPath(metadata.ConfigHash);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, metadata, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static Task DeleteMetadataAsync(string configHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = PlaywrightStoragePaths.GetSharedBrowserMetadataPath(configHash);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    internal static string GetConfigHash(PlaywrightSharedBrowserOptions options)
    {
        var raw = JsonSerializer.Serialize(new
        {
            options.Headless,
            Proxy = options.Proxy?.AbsoluteUri,
            options.UserAgent,
            Headers = options.Headers.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            options.MaxConcurrentPages,
            options.IdleTimeoutSeconds
        });

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static void ValidateOptions(PlaywrightSharedBrowserOptions options)
    {
        if (options.MaxConcurrentPages <= 0)
            throw new PlaywrightSessionException("MaxConcurrentPages must be greater than zero.");
        if (options.IdleTimeoutSeconds <= 0)
            throw new PlaywrightSessionException("IdleTimeoutSeconds must be greater than zero.");
    }

    private sealed record SharedBrowserMetadata
    {
        public required string ConfigHash { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset LastAccessedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public required int ProcessId { get; init; }
        public required int DebugPort { get; init; }
        public required bool Headless { get; init; }
        public required int MaxConcurrentPages { get; init; }
        public required int IdleTimeoutSeconds { get; init; }
        public string? Proxy { get; init; }
        public string? UserAgent { get; init; }
        public Dictionary<string, string> Headers { get; init; } = [];

        public PlaywrightSharedBrowserInfo ToInfo() => new()
        {
            ConfigHash = ConfigHash,
            CreatedAt = CreatedAt,
            LastAccessedAt = LastAccessedAt,
            ExpiresAt = ExpiresAt,
            ProcessId = ProcessId,
            DebugPort = DebugPort,
            Headless = Headless,
            MaxConcurrentPages = MaxConcurrentPages,
            Proxy = Proxy is null ? null : new Uri(Proxy)
        };
    }
}
