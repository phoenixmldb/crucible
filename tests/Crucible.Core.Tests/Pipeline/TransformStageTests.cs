namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

public class TransformStageTests
{
    [Fact]
    public async Task Execute_XmlIntermediate_ProducesHtmlFiles()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            // First run parse stage to get XML
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Test Site", baseUrl: "/",
                extensions: [], includeDrafts: false,
                ct: ct);
            parseResult.Success.Should().BeTrue();

            // Then transform
            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir,
                themePath: null, extensions: [],
                ct: ct);
            transformResult.Success.Should().BeTrue();

            File.Exists(Path.Combine(outputDir, "index.html")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "sitemap.xml")).Should().BeTrue();

            var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.html"), ct);
            html.Should().Contain("<html");
            html.Should().Contain("<title>");
        }
        finally
        {
            if (Directory.Exists(intermediateDir))
                Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_DefaultTheme_ExposesBaseUrlToScripts()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Test Site", baseUrl: "/docs/",
                extensions: [], includeDrafts: false,
                ct: ct);
            parseResult.Success.Should().BeTrue();

            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir,
                themePath: null, extensions: [],
                ct: ct);
            transformResult.Success.Should().BeTrue();

            var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.html"), ct);

            // search.js must resolve search-index.json against the deployed base URL,
            // not the server root — subpath deploys (GitHub Pages) otherwise 404.
            html.Should().Contain("window.CRUCIBLE_BASE = \"/docs/\"");
        }
        finally
        {
            if (Directory.Exists(intermediateDir))
                Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    /// <summary>
    /// ParseStage builds search-index.json, but TransformOnly is the documented
    /// extension point for injecting generated pages into the intermediate
    /// directory. A parse-time index would omit them, so TransformStage must
    /// notice the newer document and rebuild.
    /// </summary>
    [Fact]
    public async Task Execute_DocumentAddedAfterParse_IsInTheSearchIndex()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Test Site", baseUrl: "/",
                extensions: [], includeDrafts: false,
                ct: ct);
            parseResult.Success.Should().BeTrue();

            var parseTimeIndex = await File.ReadAllTextAsync(
                Path.Combine(intermediateDir, "search-index.json"), ct);
            parseTimeIndex.Should().NotContain("Generated Reference Page",
                "the page does not exist yet at parse time");

            // Simulate a generator (e.g. an API reference step) adding a page
            // to the intermediate directory between the two stages.
            await File.WriteAllTextAsync(
                Path.Combine(intermediateDir, "generated-page.xml"),
                """
                <document path="generated-page" title="Generated Reference Page"
                          description="Added after parsing">
                  <body>
                    <paragraph>SetMetadataAsync attaches metadata to a document.</paragraph>
                  </body>
                </document>
                """,
                ct);

            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir,
                themePath: null, extensions: [],
                ct: ct);
            transformResult.Success.Should().BeTrue();

            var shippedIndex = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "search-index.json"), ct);
            shippedIndex.Should().Contain("Generated Reference Page");
            shippedIndex.Should().Contain("SetMetadataAsync");

            // The pages that were present at parse time must survive the rebuild.
            shippedIndex.Should().Contain("index");
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
