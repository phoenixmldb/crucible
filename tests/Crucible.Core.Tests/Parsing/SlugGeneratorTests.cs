namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;
using Xunit;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Installation", "installation")]
    [InlineData("Getting Started", "getting-started")]
    [InlineData("xsl:for-each", "xsl-for-each")]
    [InlineData("Hello, World!", "hello-world")]
    [InlineData("  Multiple   Spaces  ", "multiple-spaces")]
    [InlineData("café", "caf")]
    [InlineData("", "")]
    [InlineData("Already-Valid", "already-valid")]
    [InlineData("123 Numbers First", "123-numbers-first")]
    public void GenerateSlug_ProducesExpectedOutput(string input, string expected)
    {
        SlugGenerator.Generate(input).Should().Be(expected);
    }
}
