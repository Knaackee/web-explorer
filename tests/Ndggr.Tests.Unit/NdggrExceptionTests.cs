using FluentAssertions;
using Xunit;

namespace Ndggr.Tests.Unit;

public class NdggrExceptionTests
{
    [Fact]
    public void NdggrException_HasMessage()
    {
        var ex = new NdggrException("something failed");

        ex.Message.Should().Be("something failed");
    }

    [Fact]
    public void NdggrException_HasInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new NdggrException("wrapper", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void SearchException_IsNdggrException()
    {
        var ex = new SearchException("search failed");

        ex.Should().BeAssignableTo<NdggrException>();
    }

    [Fact]
    public void SearchException_HasStatusCode()
    {
        var ex = new SearchException("HTTP 503", 503);

        ex.StatusCode.Should().Be(503);
        ex.Message.Should().Be("HTTP 503");
    }

    [Fact]
    public void SearchException_WithoutStatusCode_HasNullStatusCode()
    {
        var ex = new SearchException("network error");

        ex.StatusCode.Should().BeNull();
    }

    [Fact]
    public void SearchException_WithInnerException_WrapsOriginal()
    {
        var inner = new HttpRequestException("connection refused");
        var ex = new SearchException("search failed", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void RateLimitException_IsSearchException()
    {
        var ex = new RateLimitException();

        ex.Should().BeAssignableTo<SearchException>();
        ex.Should().BeAssignableTo<NdggrException>();
    }

    [Fact]
    public void RateLimitException_DefaultMessage()
    {
        var ex = new RateLimitException();

        ex.Message.Should().Contain("Rate limited");
    }

    [Fact]
    public void RateLimitException_CustomMessage()
    {
        var ex = new RateLimitException("CAPTCHA detected");

        ex.Message.Should().Be("CAPTCHA detected");
    }
}
