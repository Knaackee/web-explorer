using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebExplorer.Content;
using WebExplorer.Extensions;
using Xunit;

namespace WebExplorer.Tests.Unit.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWebExplorer_RegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddWebExplorer();

        var provider = services.BuildServiceProvider();

        provider.GetService<SearchOptions>().Should().NotBeNull();
        provider.GetService<ContentExtractionOptions>().Should().NotBeNull();
        provider.GetService<SearchClient>().Should().NotBeNull();
        provider.GetService<ContentPipeline>().Should().NotBeNull();
        provider.GetService<WebExplorerClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddWebExplorer_WithSearchOptions_RegistersCustomOptions()
    {
        var services = new ServiceCollection();
        var searchOpts = new SearchOptions { Region = "de-de" };

        services.AddWebExplorer(searchOpts);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<SearchOptions>();

        resolved.Region.Should().Be("de-de");
    }

    [Fact]
    public void AddWebExplorer_WithBothOptions_RegistersBoth()
    {
        var services = new ServiceCollection();
        var searchOpts = new SearchOptions { Region = "fr-fr" };
        var contentOpts = new ContentExtractionOptions { ChunkSize = 500 };

        services.AddWebExplorer(searchOpts, contentOpts);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<SearchOptions>().Region.Should().Be("fr-fr");
        provider.GetRequiredService<ContentExtractionOptions>().ChunkSize.Should().Be(500);
    }

    [Fact]
    public void AddWebExplorer_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddWebExplorer();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddWebExplorer_WebExplorerClient_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddWebExplorer();
        var provider = services.BuildServiceProvider();

        var client1 = provider.GetRequiredService<WebExplorerClient>();
        var client2 = provider.GetRequiredService<WebExplorerClient>();

        client1.Should().BeSameAs(client2);
    }
}
