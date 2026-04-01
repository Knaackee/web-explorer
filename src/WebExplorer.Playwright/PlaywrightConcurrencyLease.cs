namespace WebExplorer.Playwright;

internal static class PlaywrightConcurrencyLease
{
    public static async Task<IAsyncDisposable> AcquireAsync(
        string rootDirectory,
        int maxConcurrency,
        int waitTimeoutMs,
        CancellationToken cancellationToken)
    {
        var slotDirectory = Path.Combine(rootDirectory, ".slots");
        Directory.CreateDirectory(slotDirectory);

        var deadline = DateTime.UtcNow.AddMilliseconds(waitTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var index = 0; index < maxConcurrency; index++)
            {
                var slotPath = Path.Combine(slotDirectory, $"slot-{index}.lock");
                try
                {
                    var stream = new FileStream(
                        slotPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1,
                        useAsync: true);

                    return new FileLease(stream);
                }
                catch (IOException)
                {
                    // Another process holds this slot.
                }
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new PlaywrightSessionException($"Timed out waiting for a free Playwright concurrency slot in '{rootDirectory}'.");
    }

    private sealed class FileLease(FileStream stream) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
