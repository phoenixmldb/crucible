namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Models;
using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

public class ModernThemeTests
{
    private static async Task<string> BuildAndReadAsync(
        string relativeHtmlPath,
        string baseUrl = "/",
        AnalyticsConfig? analytics = null,
        SocialConfig? social = null,
        string? favicon = null)
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "modern-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Modern Site", baseUrl: baseUrl,
                extensions: [], includeDrafts: false, ct: ct).ConfigureAwait(false);
            parseResult.Success.Should().BeTrue();

            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir, themePath: "modern", extensions: [],
                analytics: analytics, ct: ct, social: social, favicon: favicon).ConfigureAwait(false);
            transformResult.Success.Should().BeTrue();

            return await File.ReadAllTextAsync(Path.Combine(outputDir, relativeHtmlPath), ct).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(intermediateDir)) Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Build_DefaultPage_IncludesRightSideToc()
    {
        var html = await BuildAndReadAsync("index.html");
        html.Should().Contain("class=\"toc\"");
        html.Should().Contain("On this page");
        html.Should().Contain("#overview");
        html.Should().Contain("#features");
    }

    [Fact]
    public async Task Build_PageWithTocFalse_OmitsRightSideToc()
    {
        var html = await BuildAndReadAsync(Path.Combine("guides", "no-toc.html"));
        html.Should().NotContain("class=\"toc\"");
    }

    [Fact]
    public async Task Build_CodeBlockWithTitle_RendersFigcaption()
    {
        var html = await BuildAndReadAsync(Path.Combine("guides", "install.html"));
        html.Should().Contain("<figure class=\"code\"");
        html.Should().Contain("<span class=\"filename\">install.sh</span>");
        html.Should().Contain("language-bash");
    }

    [Fact]
    public async Task Build_ModernTheme_RendersSearchTrigger()
    {
        var html = await BuildAndReadAsync("index.html");
        html.Should().Contain("id=\"search-trigger\"");
        html.Should().Contain("id=\"search-overlay\"");
    }

    [Fact]
    public async Task Build_ModernTheme_LinksToThemeAssets()
    {
        var html = await BuildAndReadAsync("index.html");
        html.Should().Contain("css/style.css");
        html.Should().Contain("css/prism.css");
        html.Should().Contain("js/prism.js");
        html.Should().Contain("js/search.js");
        html.Should().Contain("js/toc.js");
        html.Should().Contain("js/copy.js");
    }

    [Fact]
    public async Task Build_WithoutAnalyticsConfig_EmitsNoTrackingScript()
    {
        var html = await BuildAndReadAsync("index.html");

        // A generated site must never phone home to an ID the user did not configure.
        html.Should().NotContain("googletagmanager.com");
        html.Should().NotContain("G-FSCPKZ7RES");
        html.Should().NotContain("gtag(");
    }

    [Fact]
    public async Task Build_WithGa4Configured_EmitsThatMeasurementId()
    {
        var html = await BuildAndReadAsync("index.html",
            analytics: new AnalyticsConfig { Ga4 = "G-TESTID123" });

        html.Should().Contain("https://www.googletagmanager.com/gtag/js?id=G-TESTID123");
        html.Should().Contain("gtag('config', 'G-TESTID123')");
    }

    [Fact]
    public async Task Build_WithoutSocialConfig_EmitsATextSummaryCard()
    {
        var html = await BuildAndReadAsync("index.html");

        html.Should().Contain("<meta name=\"twitter:card\" content=\"summary\"");
        html.Should().NotContain("og:image");
        html.Should().NotContain("rel=\"icon\"");
    }

    [Fact]
    public async Task Build_WithSocialImage_EmitsASummaryCardAgainstBaseUrl()
    {
        var html = await BuildAndReadAsync("index.html", baseUrl: "https://example.test/",
            social: new SocialConfig { Image = "img/card.png", ImageAlt = "Card", TwitterSite = "@example" },
            favicon: "favicon.png");

        html.Should().Contain("<meta property=\"og:image\" content=\"https://example.test/img/card.png\"");
        html.Should().Contain("<meta name=\"twitter:card\" content=\"summary\"");
        html.Should().Contain("<meta name=\"twitter:image\" content=\"https://example.test/img/card.png\"");
        html.Should().Contain("<meta name=\"twitter:image:alt\" content=\"Card\"");
        html.Should().Contain("<meta name=\"twitter:site\" content=\"@example\"");
        html.Should().Contain("<meta property=\"og:site_name\" content=\"Modern Site\"");
        html.Should().Contain("<link rel=\"icon\" href=\"https://example.test/favicon.png\"");
    }

    [Fact]
    public async Task Build_WithAbsoluteSocialImage_UsesItAsIs()
    {
        var html = await BuildAndReadAsync("index.html", baseUrl: "https://example.test/",
            social: new SocialConfig { Image = "https://cdn.example.test/card.png" });

        html.Should().Contain("<meta property=\"og:image\" content=\"https://cdn.example.test/card.png\"");
    }

    [Fact]
    public async Task Build_WithLargeImageCard_EmitsThatCardType()
    {
        var html = await BuildAndReadAsync("index.html", baseUrl: "https://example.test/",
            social: new SocialConfig { Image = "img/banner.png", Card = "summary_large_image" });

        html.Should().Contain("<meta name=\"twitter:card\" content=\"summary_large_image\"");
    }

    [Fact]
    public async Task Build_ExposesBaseUrlToScripts()
    {
        var html = await BuildAndReadAsync(Path.Combine("guides", "install.html"),
            baseUrl: "/docs/");

        // search.js resolves search-index.json and result hrefs against this value;
        // without it, nested pages fetch a sibling path that does not exist.
        html.Should().Contain("window.CRUCIBLE_BASE = \"/docs/\"");
    }
}
