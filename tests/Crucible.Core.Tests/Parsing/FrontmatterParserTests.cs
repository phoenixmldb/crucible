namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;
using Xunit;

public class FrontmatterParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Parse_FullFrontmatter_ExtractsAllFields()
    {
        var (metadata, markdown) = FrontmatterParser.Parse(
            File.ReadAllText(FixturePath("simple-page.md")));

        metadata.Should().NotBeNull();
        metadata!.Title.Should().Be("Installation");
        metadata.Description.Should().Be("How to install PhoenixmlDb");
        metadata.Sort.Should().Be(1);
        metadata.Updated.Should().Be(new DateTime(2026, 3, 15));
        metadata.Tags.Should().BeEquivalentTo(["getting-started", "setup"]);
        markdown.Should().Contain("# Installation");
    }

    [Fact]
    public void Parse_MinimalFrontmatter_DefaultsOptionalFields()
    {
        var (metadata, markdown) = FrontmatterParser.Parse(
            File.ReadAllText(FixturePath("minimal-frontmatter.md")));

        metadata.Should().NotBeNull();
        metadata!.Title.Should().Be("Minimal Page");
        metadata.Description.Should().BeNull();
        metadata.Sort.Should().BeNull();
        metadata.Draft.Should().BeFalse();
        markdown.Should().Contain("Just a title");
    }

    [Fact]
    public void Parse_NoFrontmatter_ReturnsNull()
    {
        var (metadata, markdown) = FrontmatterParser.Parse(
            File.ReadAllText(FixturePath("no-frontmatter.md")));

        metadata.Should().BeNull();
        markdown.Should().Contain("# No Frontmatter");
    }
}
