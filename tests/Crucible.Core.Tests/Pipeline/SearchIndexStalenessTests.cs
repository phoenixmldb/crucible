namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// TransformOnly runs against a directory the caller may have added documents to after
/// parsing — the documented extension point, e.g. generated API reference pages. Deciding
/// whether to rebuild the index purely by comparing modification times is fragile in two
/// ways that both ship an index missing real pages.
/// </summary>
public sealed class SearchIndexStalenessTests : IDisposable
{
    private readonly string _intermediate =
        Path.Combine(Path.GetTempPath(), "crucible-stale-int-" + Guid.NewGuid().ToString("N"));
    private readonly string _output =
        Path.Combine(Path.GetTempPath(), "crucible-stale-out-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_intermediate)) Directory.Delete(_intermediate, recursive: true);
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task ParseFixtureAsync(CancellationToken ct)
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var parse = await ParseStage.ExecuteAsync(sourceDir, _intermediate,
            title: "Test Site", baseUrl: "/", extensions: [], includeDrafts: false, ct: ct)
            .ConfigureAwait(false);
        parse.Success.Should().BeTrue();
    }

    private async Task<string> TransformAndReadIndexAsync(CancellationToken ct)
    {
        var transform = await TransformStage.ExecuteAsync(_intermediate, _output,
            themePath: null, extensions: [], ct: ct).ConfigureAwait(false);
        transform.Success.Should().BeTrue();

        return await File.ReadAllTextAsync(
            Path.Combine(_output, "search-index.json"), ct).ConfigureAwait(false);
    }

    private async Task InjectDocumentAsync(string name, string title, CancellationToken ct) =>
        await File.WriteAllTextAsync(Path.Combine(_intermediate, name),
            $"""<document path="{Path.GetFileNameWithoutExtension(name)}" title="{title}" description=""><body><p>{title}</p></body></document>""",
            ct).ConfigureAwait(false);

    [Fact]
    public async Task DocumentWrittenInTheSameTickAsTheIndex_IsStillIndexed()
    {
        var ct = TestContext.Current.CancellationToken;
        await ParseFixtureAsync(ct);

        await InjectDocumentAsync("same-tick.xml", "Same Tick Page", ct);

        // Force the exact tie a strict `>` comparison misses. This happens for real
        // whenever the injection lands inside the index's timestamp granularity.
        var indexPath = Path.Combine(_intermediate, "search-index.json");
        var stamp = File.GetLastWriteTimeUtc(indexPath);
        File.SetLastWriteTimeUtc(Path.Combine(_intermediate, "same-tick.xml"), stamp);

        var shipped = await TransformAndReadIndexAsync(ct);

        shipped.Should().Contain("Same Tick Page");
    }

    [Fact]
    public async Task DocumentWithAnOlderTimestamp_IsStillIndexed()
    {
        var ct = TestContext.Current.CancellationToken;
        await ParseFixtureAsync(ct);

        await InjectDocumentAsync("older.xml", "Older Page", ct);

        // A file copied in with its modification time preserved is older than the index
        // that predates it. No time comparison can notice that the site grew.
        File.SetLastWriteTimeUtc(Path.Combine(_intermediate, "older.xml"),
            DateTime.UtcNow.AddDays(-1));

        var shipped = await TransformAndReadIndexAsync(ct);

        shipped.Should().Contain("Older Page");
    }
}
