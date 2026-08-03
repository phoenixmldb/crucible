namespace Crucible.Core.Tests.Search;

using Crucible.Core.Search;
using FluentAssertions;
using Xunit;

/// <summary>
/// A document that cannot be parsed is skipped — that part is correct. Skipping it
/// <i>silently</i> is not: the failure reaches users as a search box that cannot find
/// pages which visibly exist on the site, with nothing in the build output to explain it.
///
/// These tests pin the reporting, not the skipping.
/// </summary>
public sealed class DroppedDocumentReportingTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "crucible-dropped-" + Guid.NewGuid().ToString("N"));

    public DroppedDocumentReportingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void WriteGoodDocument(string name, string path) =>
        File.WriteAllText(Path.Combine(_dir, name),
            $"""<document path="{path}" title="Fine" description=""><body><p>text</p></body></document>""");

    private void WriteMalformedDocument(string name) =>
        // Unclosed <body>: XDocument.Load throws XmlException.
        File.WriteAllText(Path.Combine(_dir, name),
            """<document path="broken" title="Broken"><body><p>text</p></document>""");

    [Fact]
    public async Task BuildAsync_MalformedDocument_ReportsAWarningNamingTheFile()
    {
        WriteGoodDocument("good.xml", "guides/ok");
        WriteMalformedDocument("broken.xml");
        var warnings = new List<string>();

        await SearchIndexBuilder.BuildAsync(_dir, warnings, TestContext.Current.CancellationToken);

        warnings.Should().ContainSingle()
            .Which.Should().Contain("broken.xml",
                "a dropped document is invisible in search, so the build must say which one");
    }

    [Fact]
    public async Task BuildAsync_MalformedDocument_StillIndexesTheHealthyOnes()
    {
        WriteGoodDocument("good.xml", "guides/ok");
        WriteMalformedDocument("broken.xml");

        await SearchIndexBuilder.BuildAsync(_dir, new List<string>(),
            TestContext.Current.CancellationToken);

        var json = await File.ReadAllTextAsync(Path.Combine(_dir, "search-index.json"),
            TestContext.Current.CancellationToken);

        json.Should().Contain("guides/ok",
            "skipping the unparseable document is correct — only the silence was the defect");
    }

    [Fact]
    public async Task BuildAsync_NonDocumentRoot_ReportsAWarningNamingTheFile()
    {
        // Well-formed XML, so nothing throws — it is dropped by the root-element check
        // instead. Same symptom for the user: on the site, absent from search.
        await File.WriteAllTextAsync(Path.Combine(_dir, "stray.xml"), "<notadocument/>",
            TestContext.Current.CancellationToken);
        var warnings = new List<string>();

        await SearchIndexBuilder.BuildAsync(_dir, warnings, TestContext.Current.CancellationToken);

        warnings.Should().ContainSingle().Which.Should().Contain("stray.xml");
    }

    [Fact]
    public async Task BuildAsync_SiteManifest_IsNotReportedAsDropped()
    {
        // site-manifest.xml is deliberately excluded and must not produce noise, or the
        // warning becomes something users learn to ignore.
        await File.WriteAllTextAsync(Path.Combine(_dir, "site-manifest.xml"), "<site><page/></site>",
            TestContext.Current.CancellationToken);
        WriteGoodDocument("good.xml", "guides/ok");
        var warnings = new List<string>();

        await SearchIndexBuilder.BuildAsync(_dir, warnings, TestContext.Current.CancellationToken);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_MalformedDocument_ReportsAWarningNamingTheFile()
    {
        // llms.txt has the same swallow-everything shape as the search index, and the
        // same consequence: a page missing from the generated corpus with no explanation.
        // GenerateAsync returns early without a manifest, so it must exist or this test
        // would pass for the wrong reason.
        await File.WriteAllTextAsync(Path.Combine(_dir, "site-manifest.xml"),
            """<site base-url="/"/>""", TestContext.Current.CancellationToken);
        WriteGoodDocument("good.xml", "guides/ok");
        WriteMalformedDocument("broken.xml");
        var outDir = Path.Combine(_dir, "out");
        Directory.CreateDirectory(outDir);
        var warnings = new List<string>();

        await LlmsTxtGenerator.GenerateAsync(_dir, outDir, "Site", warnings,
            TestContext.Current.CancellationToken);

        warnings.Should().ContainSingle().Which.Should().Contain("broken.xml");
    }
}
