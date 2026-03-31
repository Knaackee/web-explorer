namespace Ndggr.Tests.Integration;

internal static class RetryHelper
{
    /// <summary>
    /// Retry a DDG search call with exponential backoff on 403/rate-limit/empty results.
    /// Creates a fresh DdgClient for each retry to avoid connection-level blocking.
    /// </summary>
    internal static async Task<DdgSearchResponse> SearchWithRetryAsync(
        Func<Task<DdgSearchResponse>> action,
        int maxAttempts = 3,
        int initialDelayMs = 5000)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await action();
                if (response.Results.Count > 0 || attempt == maxAttempts)
                    return response;

                // DDG returned 200 but no results — likely soft rate limit
                var delay = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay);
            }
            catch (SearchException) when (attempt < maxAttempts)
            {
                var delay = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay);
            }
        }

        // Final attempt — let exceptions propagate
        return await action();
    }
}
