namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// Malformed frontmatter fails the whole build. The message therefore has to say which
/// file to open, and point at a line number in that file rather than inside the block.
/// </summary>
public sealed class FrontmatterErrorTests
{
    private const string Malformed = """
        ---
        title: Fine
        tags: [unclosed
        ---

        Body.
        """;

    [Fact]
    public void Parse_MalformedYaml_NamesTheDocument()
    {
        var act = () => FrontmatterParser.Parse(Malformed, "guides/install.md");

        act.Should().Throw<FrontmatterException>()
            .WithMessage("*guides/install.md*");
    }

    [Theory]
    // An unterminated construct is only detected once input runs out, so YamlDotNet
    // reports the end of the block rather than the line that opened it. Translated into
    // the document that is the closing --- delimiter: line 4 here, line 6 below.
    [InlineData("---\ntitle: Fine\ntags: [unclosed\n---\n\nBody.\n", 4)]
    [InlineData("---\ntitle: Fine\nauthor: ok\nextra: ok\ntags: [unclosed\n---\n\nBody.\n", 6)]
    public void Parse_MalformedYaml_ReportsAFileRelativeLineNumber(string content, int expected)
    {
        // The point of the pair: the number tracks the document rather than being a
        // constant, which is what a block-relative line would look like from outside.
        var act = () => FrontmatterParser.Parse(content, "guides/install.md");

        act.Should().Throw<FrontmatterException>()
            .Which.Line.Should().Be(expected);
    }

    [Fact]
    public void Parse_MalformedYaml_KeepsTheUnderlyingCause()
    {
        var act = () => FrontmatterParser.Parse(Malformed, "guides/install.md");

        act.Should().Throw<FrontmatterException>()
            .Which.InnerException.Should().NotBeNull(
                "the original YAML diagnostic is the useful detail — wrapping must add the " +
                "file, not replace what went wrong");
    }
}

/// <summary>
/// One unparseable document must not take the build down with an unattributed stack trace.
/// </summary>
public sealed class FrontmatterBuildFailureTests : IDisposable
{
    private readonly string _source =
        Path.Combine(Path.GetTempPath(), "crucible-fm-" + Guid.NewGuid().ToString("N"));
    private readonly string _output =
        Path.Combine(Path.GetTempPath(), "crucible-fm-out-" + Guid.NewGuid().ToString("N"));

    public FrontmatterBuildFailureTests()
    {
        Directory.CreateDirectory(_source);
        File.WriteAllText(Path.Combine(_source, "good.md"),
            "---\ntitle: Good\n---\n\nFine.\n");
        File.WriteAllText(Path.Combine(_source, "bad.md"),
            "---\ntitle: Bad\ntags: [unclosed\n---\n\nBroken.\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_source)) Directory.Delete(_source, recursive: true);
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ParseStage_MalformedFrontmatter_IsAnErrorNamingTheFile()
    {
        var result = await ParseStage.ExecuteAsync(_source, _output,
            title: "Site", baseUrl: "/", extensions: [], includeDrafts: false,
            ct: TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("bad.md");
    }
}
