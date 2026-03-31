using System.Net;
using WebExplorer.Parsing;
using Primp;

namespace WebExplorer;

/// <summary>
/// Client for searching DuckDuckGo via the HTML endpoint.
/// Uses Primp (browser TLS/HTTP2 impersonation) to avoid CAPTCHA detection.
/// </summary>
public sealed class SearchClient : IDisposable
{
    private const string DdgHtmlEndpoint = "https://html.duckduckgo.com/html/";

    private readonly PrimpClient? _primpClient;
    private readonly HttpClient? _httpClient;
    private readonly HtmlResultParser _parser = new();
    private readonly bool _ownsClient;

    public SearchClient()
        : this(proxy: null)
    {
    }

    public SearchClient(SearchOptions defaultOptions)
        : this(proxy: defaultOptions.Proxy)
    {
    }

    private SearchClient(Uri? proxy)
    {
        var builder = PrimpClient.Builder()
            .WithImpersonate(Impersonate.Chrome146)
            .WithOS(ImpersonateOS.Windows)
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithCookieStore(true)
            .FollowRedirects(true);

        if (proxy is not null)
            builder = builder.WithProxy(proxy.AbsoluteUri);

        _primpClient = builder.Build();
        _ownsClient = true;
    }

    /// <summary>
    /// Internal constructor for unit testing with a mock HttpClient.
    /// </summary>
    internal SearchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    /// <summary>
    /// Search DuckDuckGo with the given query and options.
    /// </summary>
    public async Task<SearchResponse> SearchAsync(string query, SearchOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= new SearchOptions();
        var formData = BuildFormData(query, options);

        int statusCode;
        string html;

        try
        {
            if (_primpClient is not null)
            {
                var formBody = FormUrlEncode(formData);
                using var response = await _primpClient.PostAsync(
                    DdgHtmlEndpoint, formBody, "application/x-www-form-urlencoded");
                statusCode = (int)response.StatusCode;
                html = response.ReadAsString();
            }
            else
            {
                using var content = new FormUrlEncodedContent(formData);
                using var response = await _httpClient!.PostAsync(DdgHtmlEndpoint, content, cancellationToken).ConfigureAwait(false);
                statusCode = (int)response.StatusCode;
                html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new SearchException($"Search request failed: {ex.Message}", ex);
        }
        catch (PrimpException ex)
        {
            throw new SearchException($"Search request failed: {ex.Message}", ex);
        }

        if (statusCode == 429)
            throw new RateLimitException();

        if (statusCode < 200 || statusCode >= 300)
            throw new SearchException($"DuckDuckGo returned HTTP {statusCode}.", statusCode);

        // Detect CAPTCHA / bot-check pages returned by DDG
        if (IsCaptchaPage(html))
            throw new RateLimitException("DuckDuckGo returned a CAPTCHA challenge.");

        return _parser.Parse(html);
    }

    private static string FormUrlEncode(Dictionary<string, string> formData) =>
        string.Join("&", formData.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    private static Dictionary<string, string> BuildFormData(string query, SearchOptions options)
    {
        var q = options.Site is not null
            ? $"{query} site:{options.Site}"
            : query;

        var data = new Dictionary<string, string>
        {
            ["q"] = q,
            ["b"] = "",
            ["kl"] = options.Region,
            ["df"] = options.TimeFilter,
            ["kp"] = options.SafeSearch ? "1" : "-2",
            ["kh"] = "1",    // HTTPS always on
            ["kf"] = "-1",   // Disable favicons
            ["k1"] = "-1",   // Disable ads
        };

        return data;
    }

    private static bool IsCaptchaPage(string html)
    {
        // Legacy pattern: atb.js + /challenge
        if (html.Contains("atb.js", StringComparison.OrdinalIgnoreCase)
            && html.Contains("/challenge", StringComparison.OrdinalIgnoreCase))
            return true;

        // Current pattern (2025+): anomaly.js bot-check with image puzzle
        if (html.Contains("anomaly.js", StringComparison.OrdinalIgnoreCase)
            && html.Contains("challenge-form", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
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
