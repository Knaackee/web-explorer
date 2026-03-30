using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Ndggr.Content;
using Ndggr.Extensions;
using Xunit;

namespace Ndggr.Tests.Unit.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNdggr_RegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddNdggr();

        var provider = services.BuildServiceProvider();

        provider.GetService<DdgSearchOptions>().Should().NotBeNull();
        provider.GetService<ContentExtractionOptions>().Should().NotBeNull();
        provider.GetService<DdgClient>().Should().NotBeNull();
        provider.GetService<ContentPipeline>().Should().NotBeNull();
        provider.GetService<NdggrClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddNdggr_WithSearchOptions_RegistersCustomOptions()
    {
        var services = new ServiceCollection();
        var searchOpts = new DdgSearchOptions { Region = "de-de" };

        services.AddNdggr(searchOpts);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<DdgSearchOptions>();

        resolved.Region.Should().Be("de-de");
    }

    [Fact]
    public void AddNdggr_WithBothOptions_RegistersBoth()
    {
        var services = new ServiceCollection();
        var searchOpts = new DdgSearchOptions { Region = "fr-fr" };
        var contentOpts = new ContentExtractionOptions { ChunkSize = 500 };

        services.AddNdggr(searchOpts, contentOpts);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DdgSearchOptions>().Region.Should().Be("fr-fr");
        provider.GetRequiredService<ContentExtractionOptions>().ChunkSize.Should().Be(500);
    }

    [Fact]
    public void AddNdggr_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddNdggr();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddNdggr_NdggrClient_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddNdggr();
        var provider = services.BuildServiceProvider();

        var client1 = provider.GetRequiredService<NdggrClient>();
        var client2 = provider.GetRequiredService<NdggrClient>();

        client1.Should().BeSameAs(client2);
    }
}
