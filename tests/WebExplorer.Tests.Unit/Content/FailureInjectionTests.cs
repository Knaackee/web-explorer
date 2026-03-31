using System.Net;
using FluentAssertions;
using WebExplorer.Content;
using Xunit;

namespace WebExplorer.Tests.Unit.Content;

/// <summary>
/// Failure-injection tests: verify the pipeline handles errors gracefully.
/// </summary>
public class FailureInjectionTests
{
    // ── HTTP error handling ──

    [Fact]
    public async Task FetchHtml_Http429_WithRetries_RetriesExponentially()
    {
        var timestamps = new List<DateTimeOffset>();
        var handler = new SequentialHandler(
            Enumerable.Repeat((HttpStatusCode.TooManyRequests, "Rate limited"), 3).ToArray(),
            onRequest: () => timestamps.Add(DateTimeOffset.UtcNow));
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var options = new ContentExtractionOptions { MaxRetries = 2 };
        var act = () => client.FetchHtmlAsync("https://example.com", options);

        var ex = (await act.Should().ThrowAsync<ContentFetchException>()).Which;
        ex.StatusCode.Should().Be(429);
        timestamps.Should().HaveCount(3); // initial + 2 retries
    }

    [Fact]
    public async Task FetchHtml_Http500_ThenSuccess_RecoversOnRetry()
    {
        var handler = new SequentialHandler(
            (HttpStatusCode.InternalServerError, "Error"),
            (HttpStatusCode.OK, "<html><body>Recovered</body></html>"));
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var options = new ContentExtractionOptions { MaxRetries = 1 };
        var html = await client.FetchHtmlAsync("https://example.com", options);

        html.Should().Contain("Recovered");
    }

    [Fact]
    public async Task FetchHtml_Http503_AllRetries_Throws()
    {
        var callCount = 0;
        var handler = new SequentialHandler(
            Enumerable.Repeat((HttpStatusCode.ServiceUnavailable, "Unavailable"), 4).ToArray(),
            onRequest: () => callCount++);
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var options = new ContentExtractionOptions { MaxRetries = 3 };
        var act = () => client.FetchHtmlAsync("https://example.com", options);

        await act.Should().ThrowAsync<ContentFetchException>();
        callCount.Should().Be(4); // initial + 3 retries
    }

    [Fact]
    public async Task FetchHtml_Http403_NoRetry()
    {
        var callCount = 0;
        var handler = new SequentialHandler(
            [(HttpStatusCode.Forbidden, "Forbidden")],
            onRequest: () => callCount++);
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var options = new ContentExtractionOptions { MaxRetries = 2 };
        var act = () => client.FetchHtmlAsync("https://example.com", options);

        var ex = (await act.Should().ThrowAsync<ContentFetchException>()).Which;
        ex.StatusCode.Should().Be(403);
        callCount.Should().Be(1, "4xx errors (except 429) should not be retried");
    }

    [Fact]
    public async Task FetchHtml_HttpRequestException_RetriesBeforeThrowing()
    {
        var callCount = 0;
        var handler = new ThrowingHandler(() =>
        {
            callCount++;
            throw new HttpRequestException("Connection refused");
        });
        using var httpClient = new HttpClient(handler);
        using var client = new ContentFetchClient(httpClient);

        var options = new ContentExtractionOptions { MaxRetries = 1 };
        var act = () => client.FetchHtmlAsync("https://example.com", options);

        await act.Should().ThrowAsync<ContentFetchException>();
        callCount.Should().Be(2); // initial + 1 retry
    }

    // ── Broken / malformed HTML ──

    [Fact]
    public void ExtractFromHtml_BrokenHtml_DoesNotThrow()
    {
        const string brokenHtml = "<html><body><div><p>Unclosed tags<div><span>Nested badly</p></body>";
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(brokenHtml, "https://example.com/broken");

        doc.Should().NotBeNull();
        doc.Url.Should().Be("https://example.com/broken");
    }

    [Fact]
    public void ExtractFromHtml_EmptyBody_ProducesDocument()
    {
        const string emptyBody = "<html><head><title>Empty</title></head><body></body></html>";
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(emptyBody, "https://example.com/empty");

        doc.Should().NotBeNull();
        doc.Url.Should().Be("https://example.com/empty");
    }

    [Fact]
    public void ExtractFromHtml_OnlyWhitespace_ProducesDocument()
    {
        const string wsBody = "<html><body>   \n\n\t  </body></html>";
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(wsBody, "https://example.com/ws");

        doc.Should().NotBeNull();
    }

    [Fact]
    public void ExtractFromHtml_ScriptHeavy_StripsScripts()
    {
        const string scriptHtml = """
            <html><body>
            <script>alert('xss')</script>
            <p>Real content here.</p>
            <script>document.write('injected')</script>
            </body></html>
            """;
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(scriptHtml, "https://example.com/script");

        doc.Markdown.Should().NotContain("alert");
        doc.Markdown.Should().NotContain("document.write");
    }

    [Fact]
    public void ExtractFromHtml_DeepNesting_HandlesGracefully()
    {
        // Generate deeply nested HTML
        var open = string.Concat(Enumerable.Repeat("<div>", 100));
        var close = string.Concat(Enumerable.Repeat("</div>", 100));
        var deepHtml = $"<html><body>{open}<p>Deep content</p>{close}</body></html>";
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(deepHtml, "https://example.com/deep");

        doc.Should().NotBeNull();
    }

    [Fact]
    public void ExtractFromHtml_HugeInlineContent_DoesNotBlowUp()
    {
        // Simulate a page with large amount of text
        var bigText = string.Join(" ", Enumerable.Repeat("word", 10_000));
        var html = $"<html><body><article><p>{bigText}</p></article></body></html>";
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(html, "https://example.com/huge");

        doc.Should().NotBeNull();
        doc.WordCount.Should().BeGreaterThanOrEqualTo(10_000);
    }

    [Fact]
    public void ExtractFromHtml_PlainTextInput_DoesNotThrow()
    {
        const string plainText = "This is not HTML at all, just plain text.";
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(plainText, "https://example.com/plain");

        doc.Should().NotBeNull();
    }

    [Fact]
    public void ExtractFromHtml_SpecialCharacters_PreservesContent()
    {
        const string specialHtml = """
            <html><body>
            <article>
            <p>Price is &lt; $100 &amp; &gt; $50</p>
            <p>Ümlauts: ä ö ü ß</p>
            <p>Emoji: 🚀 🎉</p>
            <p>CJK: 你好世界</p>
            </article>
            </body></html>
            """;
        using var pipeline = new ContentPipeline();

        var doc = pipeline.ExtractFromHtml(specialHtml, "https://example.com/special");

        doc.Markdown.Should().Contain("$100");
        doc.Markdown.Should().Contain("ä");
    }

    // ── Pipeline-level ProcessAsync errors ──

    [Fact]
    public async Task ProcessAsync_NonExistentDomain_ThrowsContentFetchException()
    {
        using var pipeline = new ContentPipeline(new ContentExtractionOptions { MaxRetries = 0, TimeoutMs = 5000 });

        var act = () => pipeline.ProcessAsync("https://this-domain-does-not-exist-12345.example.invalid");

        await act.Should().ThrowAsync<ContentFetchException>();
    }

    [Fact]
    public async Task ProcessAsync_NullUrl_Throws()
    {
        using var pipeline = new ContentPipeline();

        var act = () => pipeline.ProcessAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessAsync_EmptyUrl_Throws()
    {
        using var pipeline = new ContentPipeline();

        var act = () => pipeline.ProcessAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Cancellation ──

    [Fact]
    public async Task FetchHtml_CancelledToken_ThrowsContentFetchException()
    {
        using var pipeline = new ContentPipeline();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => pipeline.ProcessAsync("https://example.com", cancellationToken: cts.Token);

        // TaskCanceledException is wrapped in ContentFetchException by the pipeline
        var ex = (await act.Should().ThrowAsync<ContentFetchException>()).Which;
        ex.InnerException.Should().BeAssignableTo<OperationCanceledException>();
    }

    // ── Helper classes ──

    private sealed class SequentialHandler : HttpMessageHandler
    {
        private readonly (HttpStatusCode Status, string Body)[] _responses;
        private readonly Action? _onRequest;
        private int _callIndex;

        public SequentialHandler(params (HttpStatusCode, string)[] responses)
            : this(responses, null) { }

        public SequentialHandler((HttpStatusCode, string)[] responses, Action? onRequest)
        {
            _responses = responses;
            _onRequest = onRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke();
            var idx = Math.Min(_callIndex++, _responses.Length - 1);
            var (status, body) = _responses[idx];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Action _onRequest;

        public ThrowingHandler(Action onRequest) => _onRequest = onRequest;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest();
            // Should not reach here - action should throw
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
