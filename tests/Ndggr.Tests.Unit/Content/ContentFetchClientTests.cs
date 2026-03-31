using System.Net;
using FluentAssertions;
using Ndggr.Content;
using Xunit;

namespace Ndggr.Tests.Unit.Content;

public class ContentFetchClientTests
{
    [Fact]
    public async Task FetchHtmlAsync_ThrowsOnEmptyUrl()
    {
        using var httpClient = new HttpClient();
        using var client = new ContentFetchClient(httpClient);

        var act = () => client.FetchHtmlAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FetchHtmlAsync_SuccessfulResponse_ReturnsHtml()
    {
        var handler = new FakeHandler("<html><body>Hello</body></html>");
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var html = await client.FetchHtmlAsync("https://example.com");

        html.Should().Contain("Hello");
    }

    [Fact]
    public async Task FetchHtmlAsync_Http404_ThrowsContentFetchException()
    {
        var handler = new FakeHandler("Not Found", HttpStatusCode.NotFound);
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var act = () => client.FetchHtmlAsync("https://example.com/missing");

        var ex = (await act.Should().ThrowAsync<ContentFetchException>()).Which;
        ex.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task FetchHtmlAsync_Http429_ThrowsContentFetchException()
    {
        var handler = new FakeHandler("Rate limited", HttpStatusCode.TooManyRequests);
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var act = () => client.FetchHtmlAsync("https://example.com", new ContentExtractionOptions { MaxRetries = 0 });

        var ex = (await act.Should().ThrowAsync<ContentFetchException>()).Which;
        ex.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task FetchHtmlAsync_Http500_WithRetries_RetriesBeforeThrowing()
    {
        var callCount = 0;
        var handler = new FakeHandler("Server Error", HttpStatusCode.InternalServerError, onRequest: () => callCount++);
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var options = new ContentExtractionOptions { MaxRetries = 1 };
        var act = () => client.FetchHtmlAsync("https://example.com", options);

        await act.Should().ThrowAsync<ContentFetchException>();
        callCount.Should().Be(2); // initial + 1 retry
    }

    [Fact]
    public void ContentFetchException_IsNdggrException()
    {
        var ex = new ContentFetchException("test");
        ex.Should().BeAssignableTo<NdggrException>();
    }

    [Fact]
    public void ContentExtractionException_IsNdggrException()
    {
        var ex = new ContentExtractionException("test");
        ex.Should().BeAssignableTo<NdggrException>();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        private readonly Action? _onRequest;

        public FakeHandler(string body, HttpStatusCode status = HttpStatusCode.OK, Action? onRequest = null)
        {
            _body = body;
            _status = status;
            _onRequest = onRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke();
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "text/html")
            });
        }
    }
}
