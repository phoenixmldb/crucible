namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// Pins the URL shape the themes emit.
///
/// Links are produced in five places that must agree: nav/canonical in
/// page.xslt, &lt;loc&gt; in _base/sitemap.xslt, result hrefs in search.js,
/// in-body cross-links in LinkResolver, and llms.txt in LlmsTxtGenerator.
/// Nothing asserted on link format before, so the switch to extensionless URLs
/// changed every link on every page without a single test failing — and left
/// search.js emitting ".html" against a canonical that said otherwise.
///
/// The first version of this class covered only the three XSLT/JS producers.
/// The two written in C# kept emitting ".html" for another two releases, which
/// is how pixault.dev ended up advertising 36 llms.txt URLs and 27 prose-link
/// targets that all 308 to the canonical page.
/// </summary>
public class LinkFormatTests
{
    private static async Task<(string Html, string Sitemap, string LlmsTxt)> BuildAsync(
        string theme, string relativeHtmlPath, string baseUrl = "/")
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parse = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Full Site", baseUrl: baseUrl,
                extensions: [], includeDrafts: false, ct: ct).ConfigureAwait(false);
            parse.Success.Should().BeTrue();

            var transform = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir, themePath: theme,
                extensions: [], ct: ct).ConfigureAwait(false);
            transform.Success.Should().BeTrue();

            return (
                await File.ReadAllTextAsync(Path.Combine(outputDir, relativeHtmlPath), ct).ConfigureAwait(false),
                await File.ReadAllTextAsync(Path.Combine(outputDir, "sitemap.xml"), ct).ConfigureAwait(false),
                await File.ReadAllTextAsync(Path.Combine(outputDir, "llms.txt"), ct).ConfigureAwait(false));
        }
        finally
        {
            if (Directory.Exists(intermediateDir)) Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task NavLinks_AreExtensionless(string theme)
    {
        var (html, _, _) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        html.Should().Contain("href=\"/docs/getting-started/quick-start\"");
        html.Should().NotContain("getting-started/quick-start.html");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task CanonicalAndOgUrl_AreExtensionless(string theme)
    {
        var (html, _, _) = await BuildAsync(theme,
            Path.Combine("getting-started", "quick-start.html"), baseUrl: "/docs/");

        html.Should().Contain("rel=\"canonical\" href=\"/docs/getting-started/quick-start\"");
        html.Should().Contain("content=\"/docs/getting-started/quick-start\"");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task SiteRoot_CollapsesToBaseUrl(string theme)
    {
        var (html, sitemap, _) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        // The root page's own canonical is the base URL, not "/docs/index".
        html.Should().Contain("rel=\"canonical\" href=\"/docs/\"");
        html.Should().NotContain("/docs/index\"");
        sitemap.Should().Contain("<loc>/docs/</loc>");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task SitemapLocations_AreExtensionless(string theme)
    {
        var (_, sitemap, _) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        sitemap.Should().Contain("<loc>/docs/getting-started/quick-start</loc>");
        sitemap.Should().NotContain(".html");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task BodyCrossLinks_AreExtensionless(string theme)
    {
        // The fixture homepage links to getting-started/installation.md in prose.
        // This is the producer the original version of this class did not cover.
        var (html, _, _) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        html.Should().Contain("href=\"getting-started/installation\"");
        html.Should().NotContain("getting-started/installation.html");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task RootRelativeBodyCrossLinks_AreExtensionless(string theme)
    {
        // quick-start.md links to /reference/api.md — root-relative, so it exercises
        // the other branch of LinkResolver.ResolvePath.
        var (html, _, _) = await BuildAsync(theme,
            Path.Combine("getting-started", "quick-start.html"), baseUrl: "/docs/");

        html.Should().Contain("href=\"/reference/api\"");
        html.Should().NotContain("/reference/api.html");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task LlmsTxt_IsExtensionless(string theme)
    {
        var (_, _, llmsTxt) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        llmsTxt.Should().Contain("(/docs/getting-started/quick-start)");
        llmsTxt.Should().Contain("(/docs/)",
            "the site root collapses to the base URL here exactly as it does in the sitemap");
        llmsTxt.Should().NotContain(".html");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task SearchJs_BuildsTheSameUrlShapeAsTheMarkup(string theme)
    {
        // search.js is not exercised by these tests, so pin its URL construction
        // against the XSLT rule it mirrors. If one side changes, this fails.
        var searchJs = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Themes", theme, "js", "search.js"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        // The JS must implement the same rule as c:page-url in _base/urls.xslt,
        // whose XPath pattern is: replace($path, '(^|/)index$', '$1')
        searchJs.Should().Contain("path.replace(/(^|\\/)index$/, \"$1\")",
            "result hrefs must collapse a trailing index segment exactly as c:page-url does");
        searchJs.Should().NotContain("+ \".html\"",
            "the markup emits extensionless URLs; search results must match");
    }
}
