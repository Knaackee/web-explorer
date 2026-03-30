using System.Net;
using System.Text;
using Ndggr.Parsing;

namespace Ndggr;

/// <summary>
/// Client for searching DuckDuckGo via the HTML endpoint.
/// </summary>
public sealed class DdgClient : IDisposable
{
    private const string DdgHtmlEndpoint = "https://html.duckduckgo.com/html/";

    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private readonly HtmlResultParser _parser = new();
    private readonly bool _ownsHttpClient;

    public DdgClient()
        : this(CreateDefaultHandler(proxy: null), sendUserAgent: true)
    {
    }

    public DdgClient(DdgSearchOptions defaultOptions)
        : this(CreateDefaultHandler(defaultOptions.Proxy), defaultOptions.SendUserAgent)
    {
    }

    public DdgClient(HttpMessageHandler handler, bool sendUserAgent = true)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true);
        _ownsHttpClient = true;

        ConfigureClient(sendUserAgent);
    }

    internal DdgClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = false;
    }

    /// <summary>
    /// Search DuckDuckGo with the given query and options.
    /// </summary>
    public async Task<DdgSearchResponse> SearchAsync(string query, DdgSearchOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        options ??= new DdgSearchOptions();
        var formData = BuildFormData(query, options);

        using var content = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(DdgHtmlEndpoint, content, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return _parser.Parse(html);
    }

    private static Dictionary<string, string> BuildFormData(string query, DdgSearchOptions options)
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

    private void ConfigureClient(bool sendUserAgent)
    {
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");

        if (sendUserAgent)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private static HttpClientHandler CreateDefaultHandler(Uri? proxy)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };

        if (proxy is not null)
        {
            handler.Proxy = new WebProxy(proxy);
            handler.UseProxy = true;
        }

        return handler;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
