namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

public class ParseStageTests
{
    [Fact]
    public async Task Execute_SampleSite_ProducesXmlFiles()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var result = await ParseStage.ExecuteAsync(sourceDir, outputDir,
                title: "Test Site", baseUrl: "/",
                extensions: [], includeDrafts: false,
                ct: TestContext.Current.CancellationToken);

            result.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "site-manifest.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "index.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "getting-started", "installation.xml"))
                .Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}
