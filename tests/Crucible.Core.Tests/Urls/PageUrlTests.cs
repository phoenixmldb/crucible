#pragma warning disable CA1054 // see the matching suppression on PageUrl.FromPath

namespace Crucible.Core.Tests.Urls;

using Crucible.Core.Urls;
using FluentAssertions;
using Xunit;

/// <summary>
/// Pins the C# half of the URL rule to the XSLT half.
///
/// themes/_base/urls.xslt calls itself "the single definition of how a manifest
/// @path becomes a URL", but it can only govern the producers written in XSLT:
/// nav links, canonical/og:url, and sitemap &lt;loc&gt;. Two more producers are
/// written in C# — in-body .md cross-links (LinkResolver) and llms.txt
/// (LlmsTxtGenerator) — and both emitted ".html" for as long as they existed.
///
/// On pixault.dev that put 27 distinct prose-link targets and 36 llms.txt entries
/// on URLs that 308 to the canonical one, which Search Console reported as 32
/// pages under "Page with redirect". Nothing was broken and nothing was lost: the
/// redirects resolved, and the canonical pages indexed. It cost crawl budget and
/// an extra hop on every internal link, and it buried the real findings in noise.
/// </summary>
public class PageUrlTests
{
    [Theory]
    [InlineData("index", "")]
    [InlineData("guides/install", "guides/install")]
    [InlineData("guides/index", "guides/")]
    [InlineData("a/b/c/index", "a/b/c/")]
    public void CollapseIndex_MatchesTheDocumentedXsltRule(string path, string expected) =>
        PageUrl.CollapseIndex(path).Should().Be(expected);

    [Theory]
    [InlineData("indexes", "indexes")]
    [InlineData("guides/reindex", "guides/reindex")]
    [InlineData("index/guides", "index/guides")]
    public void CollapseIndex_OnlyCollapsesAWholeTrailingSegment(string path, string expected) =>
        // The anchors in '(^|/)index$' are the whole point: "indexes" and "reindex"
        // are ordinary page names, and a leading "index/" segment is a directory.
        PageUrl.CollapseIndex(path).Should().Be(expected);

    [Theory]
    [InlineData("https://example.com/", "index", "https://example.com/")]
    [InlineData("https://example.com/", "guides/index", "https://example.com/guides/")]
    [InlineData("/docs/", "getting-started/quick-start", "/docs/getting-started/quick-start")]
    public void FromPath_ConcatenatesBaseUrlExactlyAsCPageUrlDoes(
        string baseUrl, string path, string expected) =>
        PageUrl.FromPath(baseUrl, path).Should().Be(expected);

    [Fact]
    public async Task TheXsltHalfStillImplementsTheSameRule()
    {
        // The two halves cannot share code across a language boundary, so pin them
        // by their text — the same technique LinkFormatTests uses for search.js.
        // If the XSLT rule is ever changed, this fails and names its C# mirror.
        var xslt = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Themes", "_base", "urls.xslt"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        xslt.Should().Contain("replace($path, '(^|/)index$', '$1')",
            "PageUrl.CollapseIndex is the C# mirror of this expression");
    }
}
