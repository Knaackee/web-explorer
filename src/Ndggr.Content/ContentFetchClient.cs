using System.Net;
using Primp;

namespace Ndggr.Content;

/// <summary>
/// Robust HTTP client for fetching web page content with retry, timeout, and compression.
/// Uses Primp (browser TLS/HTTP2 impersonation) to avoid bot detection.
/// </summary>
public sealed class ContentFetchClient : IDisposable
{
    private readonly PrimpClient? _primpClient;
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsClient;

    public ContentFetchClient(ContentExtractionOptions? options = null)
    {
        options ??= new ContentExtractionOptions();

        var builder = PrimpClient.Builder()
            .WithImpersonate(Impersonate.Chrome146)
            .WithOS(ImpersonateOS.Windows)
            .WithTimeout(TimeSpan.FromMilliseconds(options.TimeoutMs))
            .WithCookieStore(true)
            .FollowRedirects(true)
            .MaxRedirects(10);

        if (options.Proxy is not null)
            builder = builder.WithProxy(options.Proxy.AbsoluteUri);

        _primpClient = builder.Build();
        _ownsClient = true;
    }

    /// <summary>
    /// Internal constructor for unit testing with a mock HttpClient.
    /// </summary>
    internal ContentFetchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    /// <summary>
    /// Fetch a URL and return the HTML content as string.
    /// </summary>
    public async Task<string> FetchHtmlAsync(string url, ContentExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        options ??= new ContentExtractionOptions();

        var maxRetries = options.MaxRetries;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                int statusCode;
                string html;

                if (_primpClient is not null)
                {
                    using var response = await _primpClient.GetAsync(url);
                    statusCode = (int)response.StatusCode;
                    html = response.ReadAsString();
                }
                else
                {
                    using var response = await _httpClient!.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)response.StatusCode;
                    html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }

                if (statusCode == 429)
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw new ContentFetchException("Rate limited (HTTP 429).", 429);
                }

                if (statusCode >= 500 && attempt < maxRetries)
                {
                    await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (statusCode < 200 || statusCode >= 300)
                {
                    throw new ContentFetchException($"HTTP {statusCode} fetching {url}.", statusCode);
                }

                return html;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new ContentFetchException($"Failed to fetch {url}: {ex.Message}", ex);
            }
            catch (PrimpException) when (attempt < maxRetries)
            {
                await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
            catch (PrimpException ex)
            {
                throw new ContentFetchException($"Failed to fetch {url}: {ex.Message}", ex);
            }
        }

        throw new ContentFetchException($"Failed to fetch {url} after {maxRetries + 1} attempts.");
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _primpClient?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
