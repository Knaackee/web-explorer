using Microsoft.Extensions.DependencyInjection;
using Ndggr.Content;

namespace Ndggr.Extensions;

/// <summary>
/// DI extensions for registering ndggr services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register ndggr services with default configuration.
    /// </summary>
    public static IServiceCollection AddNdggr(this IServiceCollection services)
        => AddNdggr(services, new DdgSearchOptions(), new ContentExtractionOptions());

    /// <summary>
    /// Register ndggr services with custom search options.
    /// </summary>
    public static IServiceCollection AddNdggr(this IServiceCollection services, DdgSearchOptions searchOptions)
        => AddNdggr(services, searchOptions, new ContentExtractionOptions());

    /// <summary>
    /// Register ndggr services with full configuration.
    /// </summary>
    public static IServiceCollection AddNdggr(this IServiceCollection services, DdgSearchOptions searchOptions, ContentExtractionOptions contentOptions)
    {
        services.AddSingleton(searchOptions);
        services.AddSingleton(contentOptions);
        services.AddSingleton<DdgClient>(sp => new DdgClient(sp.GetRequiredService<DdgSearchOptions>()));
        services.AddSingleton<ContentPipeline>(sp => new ContentPipeline(sp.GetRequiredService<ContentExtractionOptions>()));
        services.AddSingleton<NdggrClient>(sp => new NdggrClient(
            sp.GetRequiredService<DdgSearchOptions>(),
            sp.GetRequiredService<ContentExtractionOptions>()));
        return services;
    }
}
