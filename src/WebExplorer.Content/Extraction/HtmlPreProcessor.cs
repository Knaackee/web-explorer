using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace WebExplorer.Content.Extraction;

/// <summary>
/// Pre-processes HTML to remove known noise elements before readability extraction.
/// Strips navigation, headers, footers, ads, cookie banners, and other boilerplate.
/// </summary>
internal static class HtmlPreProcessor
{
    /// <summary>
    /// CSS selectors for elements that should be removed before extraction.
    /// </summary>
    private static readonly string[] NoiseSelectors =
    [
        // Semantic HTML noise elements
        "nav", "header", "footer", "aside",

        // ARIA roles
        "[role='navigation']", "[role='banner']", "[role='contentinfo']",
        "[role='complementary']", "[role='search']",

        // Common class/id patterns for navigation and chrome
        ".nav", ".navbar", ".navigation", ".menu", ".sidebar",
        ".footer", ".header", ".site-header", ".site-footer",
        ".breadcrumb", ".breadcrumbs",

        // Cookie/GDPR banners
        ".cookie-banner", ".cookie-consent", ".gdpr",
        "[id*='cookie']", "[class*='cookie-consent']",

        // Ads
        ".ad", ".ads", ".advertisement", ".adsbygoogle",
        "[id*='google_ads']", "[class*='ad-slot']",

        // Social sharing
        ".social-share", ".share-buttons", ".sharing",
        "[class*='social-share']", "[class*='share-button']",

        // Related content / recommendations
        ".related-articles", ".recommended", ".related-posts",
        "[class*='related-']", "[class*='recommended']",

        // Script/style/noscript (should already be handled, but be thorough)
        "script", "style", "noscript", "svg",

        // Hidden elements
        "[hidden]", "[aria-hidden='true']",
        "[style*='display:none']", "[style*='display: none']",
        "[style*='visibility:hidden']", "[style*='visibility: hidden']"
    ];

    public static string Process(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        foreach (var selector in NoiseSelectors)
        {
            try
            {
                var elements = document.QuerySelectorAll(selector);
                foreach (var element in elements)
                {
                    element.Remove();
                }
            }
            catch
            {
                // Some selectors may not be supported; skip silently
            }
        }

        return document.DocumentElement?.OuterHtml ?? html;
    }
}
