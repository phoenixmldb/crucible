namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Models;
using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// <c>--strict</c> is advertised in <c>--help</c> as "Treat warnings as errors", and a CI
/// pipeline passing it is relying on a warning to fail the build. It has to actually do that.
/// </summary>
public sealed class StrictModeTests : IDisposable
{
    private readonly string _source =
        Path.Combine(Path.GetTempPath(), "crucible-strict-src-" + Guid.NewGuid().ToString("N"));
    private readonly string _output =
        Path.Combine(Path.GetTempPath(), "crucible-strict-out-" + Guid.NewGuid().ToString("N"));

    public StrictModeTests() => Directory.CreateDirectory(_source);

    /// <summary>
    /// A .md link to a page that does not exist. LinkResolver only checks .md targets, so
    /// the extension matters. This is a genuine defect in the source and warns.
    /// </summary>
    private void WriteBrokenLinkPage() =>
        File.WriteAllText(Path.Combine(_source, "index.md"),
            """
            ---
            title: Home
            ---

            See [the missing page](does-not-exist.md).
            """);

    /// <summary>
    /// A healthy page plus a draft. Skipping the draft is the author's intent, not a
    /// defect in the source.
    /// </summary>
    private void WriteDraftAndHealthyPage()
    {
        File.WriteAllText(Path.Combine(_source, "index.md"),
            """
            ---
            title: Home
            ---

            Nothing wrong here.
            """);
        File.WriteAllText(Path.Combine(_source, "wip.md"),
            """
            ---
            title: Work In Progress
            draft: true
            ---

            Not ready.
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_source)) Directory.Delete(_source, recursive: true);
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task<BuildResult> BuildAsync(bool strict)
    {
        var config = new CrucibleConfig
        {
            Source = _source,
            Output = _output,
            Title = "Strict Site",
            BaseUrl = "/",
        };

        var pipeline = new BuildPipeline(config, [], new BuildOptions { Strict = strict });
        return await pipeline.ExecuteAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task WithoutStrict_AWarningDoesNotFailTheBuild()
    {
        WriteBrokenLinkPage();

        var result = await BuildAsync(strict: false);

        result.Warnings.Should().NotBeEmpty("the fixture links to a page that does not exist");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WithStrict_AWarningFailsTheBuild()
    {
        WriteBrokenLinkPage();

        var result = await BuildAsync(strict: true);

        result.Warnings.Should().NotBeEmpty();
        result.Success.Should().BeFalse("--strict promises to treat warnings as errors");
    }

    [Fact]
    public async Task WithStrict_ASkippedDraftDoesNotFailTheBuild()
    {
        WriteDraftAndHealthyPage();

        var result = await BuildAsync(strict: true);

        result.Success.Should().BeTrue(
            "a draft is the author's intent, not a defect — escalating it would make " +
            "--strict unusable on any site with work in progress");
    }

    [Fact]
    public async Task ASkippedDraft_IsStillReported()
    {
        WriteDraftAndHealthyPage();

        var result = await BuildAsync(strict: false);

        result.Messages.Should().ContainSingle()
            .Which.Should().Contain("wip.md",
                "not escalating it is not the same as hiding it");
        result.Warnings.Should().BeEmpty();
    }
}
