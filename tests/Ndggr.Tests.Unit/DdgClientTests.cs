using System.Net;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ndggr.Tests.Unit;

public class DdgClientTests
{
    [Fact]
    public async Task SearchAsync_ThrowsOnNullQuery()
    {
        using var httpClient = new HttpClient();
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_ThrowsOnEmptyQuery()
    {
        using var httpClient = new HttpClient();
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_WithMockHandler_ParsesResponse()
    {
        var fixture = File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html"));
        var handler = new FakeHttpHandler(fixture);
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var response = await client.SearchAsync("test query");

        response.Results.Should().HaveCount(5);
        response.Results[0].Title.Should().Be(".NET - Wikipedia");
    }

    [Fact]
    public async Task SearchAsync_WithSiteOption_IncludesSiteInQuery()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(
            File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html")),
            onRequest: async req =>
            {
                if (req.Content is not null)
                    capturedBody = await req.Content.ReadAsStringAsync();
            });
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var options = new DdgSearchOptions { Site = "github.com" };
        await client.SearchAsync("dotnet", options);

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("site%3Agithub.com");
    }

    [Fact]
    public async Task SearchAsync_Http429_ThrowsRateLimitException()
    {
        var handler = new FakeHttpHandler("<html>rate limited</html>", statusCode: HttpStatusCode.TooManyRequests);
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync("test");

        await act.Should().ThrowAsync<RateLimitException>()
            .WithMessage("*Rate limited*");
    }

    [Fact]
    public async Task SearchAsync_Http500_ThrowsSearchException()
    {
        var handler = new FakeHttpHandler("<html>error</html>", statusCode: HttpStatusCode.InternalServerError);
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync("test");

        var ex = (await act.Should().ThrowAsync<SearchException>()).Which;
        ex.StatusCode.Should().Be(500);
        ex.Message.Should().Contain("500");
    }

    [Fact]
    public async Task SearchAsync_Http403_ThrowsSearchException()
    {
        var handler = new FakeHttpHandler("<html>forbidden</html>", statusCode: HttpStatusCode.Forbidden);
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync("test");

        var ex = (await act.Should().ThrowAsync<SearchException>()).Which;
        ex.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SearchAsync_CaptchaResponse_ThrowsRateLimitException()
    {
        // Simulates a DDG CAPTCHA page with the legacy markers
        var captchaHtml = """
            <html>
            <head><script src="atb.js"></script></head>
            <body>
            <form action="/challenge">Please complete the CAPTCHA</form>
            </body>
            </html>
            """;
        var handler = new FakeHttpHandler(captchaHtml);
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync("test");

        await act.Should().ThrowAsync<RateLimitException>()
            .WithMessage("*CAPTCHA*");
    }

    [Fact]
    public async Task SearchAsync_AnomalyCaptchaResponse_ThrowsRateLimitException()
    {
        // Simulates the current (2025+) DDG bot-check page with anomaly.js
        var captchaHtml = """
            <html>
            <body>
            <form id="challenge-form" action="//duckduckgo.com/anomaly.js?sv=html&cc=botnet" method="POST">
            <div class="anomaly-modal__title">Unfortunately, bots use DuckDuckGo too.</div>
            </form>
            </body>
            </html>
            """;
        var handler = new FakeHttpHandler(captchaHtml);
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var act = () => client.SearchAsync("test");

        await act.Should().ThrowAsync<RateLimitException>()
            .WithMessage("*CAPTCHA*");
    }

    [Fact]
    public async Task SearchAsync_UnsafeOption_SendsCorrectFormData()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(
            File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html")),
            onRequest: async req =>
            {
                if (req.Content is not null)
                    capturedBody = await req.Content.ReadAsStringAsync();
            });
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var options = new DdgSearchOptions { SafeSearch = false };
        await client.SearchAsync("test", options);

        capturedBody.Should().NotBeNull();
        // kp=-2 means safe search off
        capturedBody.Should().Contain("kp=-2");
    }

    [Fact]
    public async Task SearchAsync_SafeSearchDefault_SendsKp1()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(
            File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html")),
            onRequest: async req =>
            {
                if (req.Content is not null)
                    capturedBody = await req.Content.ReadAsStringAsync();
            });
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        await client.SearchAsync("test");

        capturedBody.Should().NotBeNull();
        // kp=1 means safe search on
        capturedBody.Should().Contain("kp=1");
    }

    [Fact]
    public async Task SearchAsync_WithRegion_SendsCorrectKl()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(
            File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html")),
            onRequest: async req =>
            {
                if (req.Content is not null)
                    capturedBody = await req.Content.ReadAsStringAsync();
            });
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var options = new DdgSearchOptions { Region = "de-de" };
        await client.SearchAsync("test", options);

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("kl=de-de");
    }

    [Fact]
    public async Task SearchAsync_WithTimeFilter_SendsDf()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(
            File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html")),
            onRequest: async req =>
            {
                if (req.Content is not null)
                    capturedBody = await req.Content.ReadAsStringAsync();
            });
        using var httpClient = new HttpClient(handler);
        using var client = new DdgClient(httpClient);

        var options = new DdgSearchOptions { TimeFilter = "w" };
        await client.SearchAsync("test", options);

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("df=w");
    }

    [Fact]
    public void DdgSearchOptions_WithProxy_StoresUri()
    {
        var options = new DdgSearchOptions { Proxy = new Uri("http://proxy:8080") };

        options.Proxy.Should().NotBeNull();
        options.Proxy!.Host.Should().Be("proxy");
    }

    [Fact]
    public void DdgSearchOptions_Defaults_AreCorrect()
    {
        var options = new DdgSearchOptions();

        options.Region.Should().Be("us-en");
        options.SafeSearch.Should().BeTrue();
        options.NumResults.Should().Be(10);
    }

    internal sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _responseHtml;
        private readonly HttpStatusCode _statusCode;
        private readonly Func<HttpRequestMessage, Task>? _onRequest;

        public FakeHttpHandler(string responseHtml, HttpStatusCode statusCode = HttpStatusCode.OK, Func<HttpRequestMessage, Task>? onRequest = null)
        {
            _responseHtml = responseHtml;
            _statusCode = statusCode;
            _onRequest = onRequest;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_onRequest is not null)
                await _onRequest(request);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseHtml, System.Text.Encoding.UTF8, "text/html")
            };
        }
    }
}
