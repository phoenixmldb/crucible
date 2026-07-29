namespace Crucible.Core.Tests.Pipeline;

using System.Xml.Linq;
using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// A page that is generated but absent from sitemap.xml is invisible to
/// crawlers. This happened to every section landing page: SiteManifestBuilder
/// treats a subdirectory's index.md as section metadata and omits it from the
/// manifest, and the sitemap is generated from that same manifest — so pages
/// excluded from navigation were silently excluded from SEO too. On
/// phoenixml.dev that was 15 of 112 pages.
/// </summary>
public class SitemapCoverageTests
{
    private static async Task<(List<string> Pages, List<string> Locations)> BuildAsync(
        string theme, string baseUrl = "/")
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parse = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Full Site", baseUrl: baseUrl,
                extensions: [], includeDrafts: false, ct: ct).ConfigureAwait(true);
            parse.Success.Should().BeTrue();

            var transform = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir, themePath: theme,
                extensions: [], ct: ct).ConfigureAwait(true);
            transform.Success.Should().BeTrue();

            var pages = Directory
                .GetFiles(outputDir, "*.html", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(outputDir, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList();

            var sitemapXml = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "sitemap.xml"), ct).ConfigureAwait(true);
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var locations = XDocument.Parse(sitemapXml)
                .Descendants(ns + "loc")
                .Select(e => e.Value)
                .Order(StringComparer.Ordinal)
                .ToList();

            return (pages, locations);
        }
        finally
        {
            if (Directory.Exists(intermediateDir)) Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    /// <summary>Maps a generated file to the URL the themes link it by.</summary>
    private static string ExpectedUrl(string relativeHtmlPath, string baseUrl)
    {
        var path = relativeHtmlPath[..^".html".Length];
        if (path == "index") return baseUrl;
        if (path.EndsWith("/index", StringComparison.Ordinal))
            return baseUrl + path[..^"index".Length];
        return baseUrl + path;
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task EveryGeneratedPage_AppearsInTheSitemap(string theme)
    {
        var (pages, locations) = await BuildAsync(theme, "/docs/");

        pages.Should().NotBeEmpty();
        var expected = pages.Select(p => ExpectedUrl(p, "/docs/")).Order(StringComparer.Ordinal);

        locations.Should().BeEquivalentTo(expected,
            "a generated page missing from sitemap.xml is invisible to crawlers");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task SectionLandingPage_IsListed_AsADirectoryUrl(string theme)
    {
        var (pages, locations) = await BuildAsync(theme, "/docs/");

        pages.Should().Contain("getting-started/index.html",
            "the fixture has a section landing page");

        // A trailing "index" segment collapses to the directory, so the landing
        // page and the section share one canonical URL.
        locations.Should().Contain("/docs/getting-started/");
        locations.Should().NotContain("/docs/getting-started/index");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task Sitemap_HasNoDuplicateLocations(string theme)
    {
        var (_, locations) = await BuildAsync(theme, "/docs/");

        locations.Should().OnlyHaveUniqueItems();
    }
}
