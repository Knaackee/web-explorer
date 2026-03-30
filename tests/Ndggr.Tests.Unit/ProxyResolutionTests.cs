using FluentAssertions;
using Xunit;

namespace Ndggr.Tests.Unit;

public class ProxyResolutionTests
{
    [Fact]
    public void ResolveProxy_ExplicitProxy_ReturnsExplicitUri()
    {
        var result = ProxyResolver.Resolve("http://myproxy:3128");

        result.Should().NotBeNull();
        result!.Host.Should().Be("myproxy");
        result.Port.Should().Be(3128);
    }

    [Fact]
    public void ResolveProxy_NullExplicit_NoEnvVar_ReturnsNull()
    {
        // Ensure env var is cleared for this test
        var original = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        var originalLower = Environment.GetEnvironmentVariable("https_proxy");
        try
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", null);
            Environment.SetEnvironmentVariable("https_proxy", null);

            var result = ProxyResolver.Resolve(null);

            result.Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", original);
            Environment.SetEnvironmentVariable("https_proxy", originalLower);
        }
    }

    [Fact]
    public void ResolveProxy_NullExplicit_WithEnvVar_ReturnsEnvUri()
    {
        var original = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        try
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://envproxy:8080");

            var result = ProxyResolver.Resolve(null);

            result.Should().NotBeNull();
            result!.Host.Should().Be("envproxy");
            result.Port.Should().Be(8080);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", original);
        }
    }

    [Fact]
    public void ResolveProxy_ExplicitProxy_TakesPriorityOverEnvVar()
    {
        var original = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        try
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://envproxy:8080");

            var result = ProxyResolver.Resolve("http://explicit:3128");

            result.Should().NotBeNull();
            result!.Host.Should().Be("explicit");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", original);
        }
    }

    [Fact]
    public void ResolveProxy_EmptyExplicit_FallsBackToEnvVar()
    {
        var original = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        try
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://envproxy:9090");

            var result = ProxyResolver.Resolve("");

            result.Should().NotBeNull();
            result!.Host.Should().Be("envproxy");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", original);
        }
    }

    [Fact]
    public void ResolveProxy_LowercaseEnvVar_IsAlsoChecked()
    {
        var originalUpper = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        var originalLower = Environment.GetEnvironmentVariable("https_proxy");
        try
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", null);
            Environment.SetEnvironmentVariable("https_proxy", "http://lowerproxy:7070");

            var result = ProxyResolver.Resolve(null);

            result.Should().NotBeNull();
            result!.Host.Should().Be("lowerproxy");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", originalUpper);
            Environment.SetEnvironmentVariable("https_proxy", originalLower);
        }
    }
}
