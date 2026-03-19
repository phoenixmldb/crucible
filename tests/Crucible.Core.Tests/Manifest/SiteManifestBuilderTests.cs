namespace Crucible.Core.Tests.Manifest;

using Crucible.Core.Manifest;
using Crucible.Core.Models;
using FluentAssertions;
using Xunit;

public class SiteManifestBuilderTests
{
    private static string FixtureDir(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Build_SampleSite_ProducesCorrectStructure()
    {
        var manifest = SiteManifestBuilder.Build(FixtureDir("sample-site"),
            title: "Test Site", baseUrl: "/");

        manifest.Title.Should().Be("Test Site");
        manifest.Children.Should().HaveCount(3); // index page + 2 sections

        var index = manifest.Children.OfType<SitePage>().First();
        index.Title.Should().Be("Home");
        index.Sort.Should().Be(0);
    }

    [Fact]
    public void Build_SortOrder_SortedPagesBeforeUnsorted()
    {
        var manifest = SiteManifestBuilder.Build(FixtureDir("sample-site"),
            title: "Test", baseUrl: "/");

        var gettingStarted = manifest.Children.OfType<SiteSection>()
            .First(s => s.Path.Contains("getting-started", StringComparison.Ordinal));
        gettingStarted.Children.Should().HaveCount(2);
        gettingStarted.Children[0].Title.Should().Be("Installation"); // sort=1
        gettingStarted.Children[1].Title.Should().Be("Quick Start");  // sort=2
    }

    [Fact]
    public void Build_DirectoryWithoutIndex_InfersTitleFromDirName()
    {
        var manifest = SiteManifestBuilder.Build(FixtureDir("sample-site"),
            title: "Test", baseUrl: "/");

        var reference = manifest.Children.OfType<SiteSection>()
            .First(s => s.Path.Contains("reference", StringComparison.Ordinal));
        reference.Title.Should().Be("Reference");
    }
}
