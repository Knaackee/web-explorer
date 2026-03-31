using Microsoft.Extensions.DependencyInjection;
using WebExplorer.Content;

namespace WebExplorer.Extensions;

/// <summary>
/// DI extensions for registering web-explorer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register web-explorer services with default configuration.
    /// </summary>
    public static IServiceCollection AddWebExplorer(this IServiceCollection services)
        => AddWebExplorer(services, new SearchOptions(), new ContentExtractionOptions());

    /// <summary>
    /// Register web-explorer services with custom search options.
    /// </summary>
    public static IServiceCollection AddWebExplorer(this IServiceCollection services, SearchOptions searchOptions)
        => AddWebExplorer(services, searchOptions, new ContentExtractionOptions());

    /// <summary>
    /// Register web-explorer services with full configuration.
    /// </summary>
    public static IServiceCollection AddWebExplorer(this IServiceCollection services, SearchOptions searchOptions, ContentExtractionOptions contentOptions)
    {
        services.AddSingleton(searchOptions);
        services.AddSingleton(contentOptions);
        services.AddSingleton<SearchClient>(sp => new SearchClient(sp.GetRequiredService<SearchOptions>()));
        services.AddSingleton<ContentPipeline>(sp => new ContentPipeline(sp.GetRequiredService<ContentExtractionOptions>()));
        services.AddSingleton<WebExplorerClient>(sp => new WebExplorerClient(
            sp.GetRequiredService<SearchOptions>(),
            sp.GetRequiredService<ContentExtractionOptions>()));
        return services;
    }
}
