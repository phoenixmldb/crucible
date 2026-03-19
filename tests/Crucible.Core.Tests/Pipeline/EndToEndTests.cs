namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Models;
using Crucible.Core.Pipeline;
using Crucible.Extensions;
using FluentAssertions;
using Xunit;

public class EndToEndTests
{
    [Fact]
    public async Task FullBuild_ProducesValidStaticSite()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var config = new CrucibleConfig
            {
                Title = "Test Documentation",
                BaseUrl = "/",
                Source = sourceDir,
                Output = outputDir
            };

            var pipeline = new BuildPipeline(config,
                ExtensionRegistry.DefaultExtensions,
                new BuildOptions());

            var result = await pipeline.ExecuteAsync(
                TestContext.Current.CancellationToken);

            result.Success.Should().BeTrue(
                because: string.Join(", ", result.Errors));

            // HTML files exist
            File.Exists(Path.Combine(outputDir, "index.html")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "getting-started", "installation.html"))
                .Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "getting-started", "quick-start.html"))
                .Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "reference", "api.html"))
                .Should().BeTrue();

            // Sitemap exists
            File.Exists(Path.Combine(outputDir, "sitemap.xml")).Should().BeTrue();

            // CSS assets copied
            File.Exists(Path.Combine(outputDir, "css", "style.css")).Should().BeTrue();

            // HTML is valid
            var indexHtml = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "index.html"),
                TestContext.Current.CancellationToken);
            indexHtml.Should().Contain("<html");
            indexHtml.Should().Contain("og:title");
            indexHtml.Should().Contain("<nav");
            indexHtml.Should().Contain("<main");

            // TODO: Internal links should be rewritten from .md to .html
            // once LinkResolver is integrated into the transform stage.
            // indexHtml.Should().NotContain(".md\"");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task StagedBuild_ParseThenTransform_ProducesHtml()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Stage 1: Parse
            var parseResult = await ParseStage.ExecuteAsync(
                sourceDir, intermediateDir,
                title: "Test", baseUrl: "/",
                extensions: ExtensionRegistry.DefaultExtensions,
                includeDrafts: false,
                ct: TestContext.Current.CancellationToken);
            parseResult.Success.Should().BeTrue(
                because: string.Join(", ", parseResult.Errors));
            File.Exists(Path.Combine(intermediateDir, "site-manifest.xml"))
                .Should().BeTrue();

            // Verify XML intermediate has mermaid element
            var quickStartXml = await File.ReadAllTextAsync(
                Path.Combine(intermediateDir, "getting-started", "quick-start.xml"),
                TestContext.Current.CancellationToken);
            quickStartXml.Should().Contain("<mermaid>");

            // Stage 2: Transform
            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir,
                themePath: null,
                extensions: ExtensionRegistry.DefaultExtensions,
                ct: TestContext.Current.CancellationToken);
            transformResult.Success.Should().BeTrue(
                because: string.Join(", ", transformResult.Errors));
            File.Exists(Path.Combine(outputDir, "index.html")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(intermediateDir))
                Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}
