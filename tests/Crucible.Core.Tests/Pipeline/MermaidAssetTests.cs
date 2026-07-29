namespace Crucible.Core.Tests.Pipeline;

using System.Text.RegularExpressions;
using Crucible.Core.Pipeline;
using Crucible.Extensions;
using FluentAssertions;
using Xunit;

/// <summary>
/// The mermaid runtime used to be emitted by the per-diagram template, so a
/// page with N diagrams pulled the same multi-megabyte CDN script N times, and
/// the extension's own init script was never referenced at all. Both themes now
/// include it once per page, guarded on the page actually having a diagram.
/// </summary>
public class MermaidAssetTests
{
    private static async Task<string> BuildAndReadAsync(string theme, string relativeHtmlPath)
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Full Site", baseUrl: "/",
                extensions: ExtensionRegistry.DefaultExtensions,
                includeDrafts: false, ct: ct).ConfigureAwait(false);
            parseResult.Success.Should().BeTrue();

            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir, themePath: theme,
                extensions: ExtensionRegistry.DefaultExtensions, ct: ct).ConfigureAwait(false);
            transformResult.Success.Should().BeTrue();

            return await File.ReadAllTextAsync(
                Path.Combine(outputDir, relativeHtmlPath), ct).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(intermediateDir)) Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    private static int Count(string haystack, string needle) =>
        Regex.Count(haystack, Regex.Escape(needle));

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task PageWithTwoDiagrams_IncludesRuntimeExactlyOnce(string theme)
    {
        var html = await BuildAndReadAsync(theme,
            Path.Combine("getting-started", "two-diagrams.html"));

        Count(html, "<pre class=\"mermaid\">").Should().Be(2, "the fixture has two diagrams");
        Count(html, "npm/mermaid@").Should().Be(1, "the runtime is a per-page include");
        Count(html, "js/mermaid-init.js").Should().Be(1);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task PageWithoutDiagrams_DoesNotLoadMermaidAtAll(string theme)
    {
        var html = await BuildAndReadAsync(theme, "index.html");

        html.Should().NotContain("mermaid");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task MermaidRuntime_IsVersionPinnedWithIntegrity(string theme)
    {
        var html = await BuildAndReadAsync(theme,
            Path.Combine("getting-started", "two-diagrams.html"));

        // An unpinned CDN URL silently changes under the site; SRI makes a
        // tampered payload fail closed instead of executing.
        html.Should().MatchRegex(@"npm/mermaid@\d+\.\d+\.\d+/");
        html.Should().Contain("integrity=\"sha384-");
        html.Should().Contain("crossorigin=\"anonymous\"");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("modern")]
    public async Task SearchRuntime_IsServedLocally_NotFromACdn(string theme)
    {
        var html = await BuildAndReadAsync(theme, "index.html");

        html.Should().Contain("js/lunr.js");
        html.Should().NotContain("unpkg.com");
    }
}
