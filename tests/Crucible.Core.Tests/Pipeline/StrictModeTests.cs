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

    public StrictModeTests()
    {
        Directory.CreateDirectory(_source);
        // A .md link to a page that does not exist. LinkResolver only checks .md targets,
        // so the extension matters. MarkdownToXmlEmitter reports this as a warning, not an
        // error, so the build succeeds today.
        File.WriteAllText(Path.Combine(_source, "index.md"),
            """
            ---
            title: Home
            ---

            See [the missing page](does-not-exist.md).
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
        var result = await BuildAsync(strict: false);

        result.Warnings.Should().NotBeEmpty("the fixture links to a page that does not exist");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WithStrict_AWarningFailsTheBuild()
    {
        var result = await BuildAsync(strict: true);

        result.Warnings.Should().NotBeEmpty();
        result.Success.Should().BeFalse("--strict promises to treat warnings as errors");
    }
}
