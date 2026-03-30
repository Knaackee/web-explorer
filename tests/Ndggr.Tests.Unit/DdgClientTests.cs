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
        using var client = new DdgClient();

        var act = () => client.SearchAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_ThrowsOnEmptyQuery()
    {
        using var client = new DdgClient();

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

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _responseHtml;
        private readonly Func<HttpRequestMessage, Task>? _onRequest;

        public FakeHttpHandler(string responseHtml, Func<HttpRequestMessage, Task>? onRequest = null)
        {
            _responseHtml = responseHtml;
            _onRequest = onRequest;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_onRequest is not null)
                await _onRequest(request);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseHtml, System.Text.Encoding.UTF8, "text/html")
            };
        }
    }
}
