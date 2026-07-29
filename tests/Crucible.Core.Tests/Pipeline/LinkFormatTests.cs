namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// Pins the URL shape the themes emit.
///
/// Links are produced in three places that must agree: nav/canonical in
/// page.xslt, &lt;loc&gt; in _base/sitemap.xslt, and result hrefs in search.js.
/// Nothing asserted on link format before, so the switch to extensionless URLs
/// changed every link on every page without a single test failing — and left
/// search.js emitting ".html" against a canonical that said otherwise.
/// </summary>
public class LinkFormatTests
{
    private static async Task<(string Html, string Sitemap)> BuildAsync(
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
                await File.ReadAllTextAsync(Path.Combine(outputDir, "sitemap.xml"), ct).ConfigureAwait(false));
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
        var (html, _) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        html.Should().Contain("href=\"/docs/getting-started/quick-start\"");
        html.Should().NotContain("getting-started/quick-start.html");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task CanonicalAndOgUrl_AreExtensionless(string theme)
    {
        var (html, _) = await BuildAsync(theme,
            Path.Combine("getting-started", "quick-start.html"), baseUrl: "/docs/");

        html.Should().Contain("rel=\"canonical\" href=\"/docs/getting-started/quick-start\"");
        html.Should().Contain("content=\"/docs/getting-started/quick-start\"");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task SiteRoot_CollapsesToBaseUrl(string theme)
    {
        var (html, sitemap) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

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
        var (_, sitemap) = await BuildAsync(theme, "index.html", baseUrl: "/docs/");

        sitemap.Should().Contain("<loc>/docs/getting-started/quick-start</loc>");
        sitemap.Should().NotContain(".html");
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

        searchJs.Should().Contain("path === \"index\" ? \"\" : path",
            "result hrefs must collapse the site root exactly as page.xslt does");
        searchJs.Should().NotContain("+ \".html\"",
            "the markup emits extensionless URLs; search results must match");
    }
}
