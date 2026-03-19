namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

public class InputDetectorTests
{
    [Fact]
    public void Detect_DirectoryWithManifest_ReturnsXmlIntermediate()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site-manifest.xml"), "<site/>");
        try
        {
            InputDetector.Detect(dir).Should().Be(InputType.XmlIntermediate);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Detect_DirectoryWithMarkdown_ReturnsMarkdownSource()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.md"), "# Hello");
        try
        {
            InputDetector.Detect(dir).Should().Be(InputType.MarkdownSource);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
