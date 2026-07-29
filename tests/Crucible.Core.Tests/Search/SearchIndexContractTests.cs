namespace Crucible.Core.Tests.Search;

using System.Text.Json;
using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

/// <summary>
/// Pins the on-disk shape of search-index.json.
///
/// Both themes' search.js parse this file by hand, so its structure is a public
/// contract between the C# builder and the shipped JavaScript. The modern theme
/// shipped broken search for an entire release because it expected
/// <c>{ "documents": [...] }</c> while the builder emits a bare array — these
/// tests exist so that mismatch fails the build instead of the user's site.
/// </summary>
public class SearchIndexContractTests
{
    private static async Task<JsonElement> BuildIndexAsync()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "modern-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Modern Site", baseUrl: "/",
                extensions: [], includeDrafts: false, ct: ct).ConfigureAwait(false);
            parseResult.Success.Should().BeTrue();

            var json = await File.ReadAllTextAsync(
                Path.Combine(intermediateDir, "search-index.json"), ct).ConfigureAwait(false);

            return JsonDocument.Parse(json).RootElement.Clone();
        }
        finally
        {
            if (Directory.Exists(intermediateDir)) Directory.Delete(intermediateDir, recursive: true);
        }
    }

    [Fact]
    public async Task SearchIndex_IsTopLevelArray_NotWrapperObject()
    {
        var root = await BuildIndexAsync();

        root.ValueKind.Should().Be(JsonValueKind.Array,
            "both themes' search.js iterate the parsed JSON directly");
        root.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchIndex_EntriesExposeTheFieldsThemesIndex()
    {
        var root = await BuildIndexAsync();
        var entry = root[0];

        // Field names are camelCase (JsonKnownNamingPolicy.CamelCase) and are read
        // verbatim by search.js in both themes.
        entry.TryGetProperty("path", out var path).Should().BeTrue();
        entry.TryGetProperty("title", out _).Should().BeTrue();
        entry.TryGetProperty("description", out _).Should().BeTrue();
        entry.TryGetProperty("headings", out var headings).Should().BeTrue();
        entry.TryGetProperty("body", out var body).Should().BeTrue();

        path.ValueKind.Should().Be(JsonValueKind.String);
        headings.ValueKind.Should().Be(JsonValueKind.Array,
            "search.js joins headings into a single lunr field");
        body.ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task SearchIndex_HasNoContentField()
    {
        var root = await BuildIndexAsync();

        // The modern theme used to read `d.content`, which never existed. If a
        // `content` field is ever added, the snippet code must be revisited.
        root[0].TryGetProperty("content", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SearchIndex_PathsAreExtensionless_AndForwardSlashed()
    {
        var root = await BuildIndexAsync();

        var paths = root.EnumerateArray()
            .Select(e => e.GetProperty("path").GetString())
            .ToList();

        // search.js builds hrefs as base + path + ".html".
        paths.Should().Contain("guides/install");
        paths.Should().AllSatisfy(p =>
        {
            p.Should().NotEndWith(".html");
            p.Should().NotContain("\\");
        });
    }
}
