namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;
using Xunit;

/// <summary>
/// The closing delimiter is a line that is exactly <c>---</c>. Matching any line that
/// merely starts with it lets a horizontal rule or a typo end the block early, which
/// truncates the metadata and leaks the remainder into the body.
/// </summary>
public sealed class FrontmatterDelimiterTests
{
    [Theory]
    [InlineData("----", "a four-dash rule")]
    [InlineData("---not-a-delimiter", "a delimiter with trailing text")]
    public void MalformedClosingDelimiter_IsNotTreatedAsTheTerminator(string delimiter, string _)
    {
        var content = $"---\ntitle: Real\n{delimiter}\n\nBody.\n";

        var (metadata, markdown) = FrontmatterParser.Parse(content, "doc.md");

        // Previously the block ended at this line, and whatever followed the first three
        // dashes leaked into the rendered page — "-" for a four-dash rule. There is now no
        // terminator at all, so the document reports as having no frontmatter and the
        // author gets told, rather than getting a silently mangled page.
        metadata.Should().BeNull();
        markdown.Should().Be(content);
    }

    [Fact]
    public void WellFormedDelimiterWithTrailingWhitespace_StillTerminates()
    {
        var content = "---\ntitle: Real\nsort: 3\n---   \n\nBody.\n";

        var (metadata, markdown) = FrontmatterParser.Parse(content, "doc.md");

        metadata.Should().NotBeNull();
        metadata!.Sort.Should().Be(3, "trailing spaces are invisible and must not break the build");
        markdown.Should().StartWith("Body.");
    }

    [Fact]
    public void CrlfLineEndings_Parse()
    {
        var content = "---\r\ntitle: Windows\r\nsort: 4\r\n---\r\n\r\nBody.\r\n";

        var (metadata, markdown) = FrontmatterParser.Parse(content, "doc.md");

        metadata.Should().NotBeNull();
        metadata!.Title.Should().Be("Windows");
        metadata.Sort.Should().Be(4);
        markdown.Should().StartWith("Body.");
    }

    [Fact]
    public void MissingTerminator_YieldsNoMetadataAndLeavesContentAlone()
    {
        var content = "---\ntitle: Unterminated\n\nBody without a closing delimiter.\n";

        var (metadata, markdown) = FrontmatterParser.Parse(content, "doc.md");

        metadata.Should().BeNull();
        markdown.Should().Be(content);
    }

    [Fact]
    public void HorizontalRuleInBody_DoesNotAffectParsing()
    {
        var content = "---\ntitle: Real\n---\n\nAbove.\n\n---\n\nBelow.\n";

        var (metadata, markdown) = FrontmatterParser.Parse(content, "doc.md");

        metadata!.Title.Should().Be("Real");
        markdown.Should().Contain("Above.").And.Contain("Below.");
    }

    [Fact]
    public void EmptyFrontmatterBlock_YieldsNoMetadata()
    {
        var content = "---\n---\n\nBody.\n";

        var (metadata, markdown) = FrontmatterParser.Parse(content, "doc.md");

        metadata.Should().BeNull("an empty block declares nothing");
        markdown.Should().StartWith("Body.");
    }
}
