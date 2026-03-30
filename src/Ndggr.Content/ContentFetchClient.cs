using System.Net;

namespace Ndggr.Content;

/// <summary>
/// Robust HTTP client for fetching web page content with retry, timeout, and compression.
/// </summary>
public sealed class ContentFetchClient : IDisposable
{
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ContentFetchClient(ContentExtractionOptions? options = null)
    {
        options ??= new ContentExtractionOptions();
        var handler = CreateHandler(options);
        _httpClient = new HttpClient(handler, disposeHandler: true);
        _ownsHttpClient = true;
        ConfigureClient(options);
    }

    internal ContentFetchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = false;
    }

    /// <summary>
    /// Fetch a URL and return the HTML content as string.
    /// </summary>
    public async Task<string> FetchHtmlAsync(string url, ContentExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        options ??= new ContentExtractionOptions();

        var maxRetries = options.MaxRetries;
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                foreach (var header in options.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    response.Dispose();
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw new ContentFetchException("Rate limited (HTTP 429).", 429);
                }

                if (response.StatusCode >= HttpStatusCode.InternalServerError && attempt < maxRetries)
                {
                    response.Dispose();
                    await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    response.Dispose();
                    throw new ContentFetchException($"HTTP {code} fetching {url}.", code);
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                response?.Dispose();
                await Task.Delay(1000 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new ContentFetchException($"Failed to fetch {url}: {ex.Message}", ex);
            }
        }

        throw new ContentFetchException($"Failed to fetch {url} after {maxRetries + 1} attempts.");
    }

    private static HttpClientHandler CreateHandler(ContentExtractionOptions options)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };

        if (options.Proxy is not null)
        {
            handler.Proxy = new WebProxy(options.Proxy);
            handler.UseProxy = true;
        }

        return handler;
    }

    private void ConfigureClient(ContentExtractionOptions options)
    {
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");

        var ua = options.UserAgent ?? DefaultUserAgent;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ua);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
