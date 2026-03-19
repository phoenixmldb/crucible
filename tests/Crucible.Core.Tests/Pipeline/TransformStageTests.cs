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
}
