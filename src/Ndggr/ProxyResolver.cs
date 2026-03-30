namespace Ndggr;

/// <summary>
/// Resolves proxy URIs with fallback to environment variables.
/// Priority: explicit value > HTTPS_PROXY > https_proxy.
/// </summary>
public static class ProxyResolver
{
    public static Uri? Resolve(string? explicitProxy)
    {
        if (!string.IsNullOrWhiteSpace(explicitProxy))
            return new Uri(explicitProxy);

        var envProxy = Environment.GetEnvironmentVariable("HTTPS_PROXY")
                    ?? Environment.GetEnvironmentVariable("https_proxy");

        if (!string.IsNullOrWhiteSpace(envProxy))
            return new Uri(envProxy);

        return null;
    }
}
