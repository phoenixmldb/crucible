namespace Crucible.Core.Pipeline;

using System.Diagnostics;
using System.Xml.Linq;
using Crucible.Core.Extensions;
using Crucible.Core.Manifest;
using Crucible.Core.Models;
using Crucible.Core.Parsing;
using Crucible.Core.Search;

#pragma warning disable CA1054 // URI parameters should not be strings — baseUrl is a path prefix, not a full URI

public static class ParseStage
{
    public static async Task<BuildResult> ExecuteAsync(
        string sourceDir,
        string outputDir,
        string title,
        string baseUrl,
        IEnumerable<ICrucibleExtension> extensions,
        bool includeDrafts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceDir);
        ArgumentNullException.ThrowIfNull(outputDir);
        ArgumentNullException.ThrowIfNull(extensions);

        var result = new BuildResult();
        var timer = Stopwatch.StartNew();

        // 1. Discover all .md files recursively
        var mdFiles = Directory.GetFiles(sourceDir, "*.md", SearchOption.AllDirectories);

        // 2-4. Parse each file, skip drafts, collect errors
        var validFiles = new List<(string FilePath, DocumentMetadata Metadata, string Markdown)>();

        foreach (var file in mdFiles)
        {
            ct.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var (metadata, markdown) = FrontmatterParser.Parse(content);

            if (metadata == null)
            {
                result.Errors.Add($"Missing frontmatter: {Path.GetRelativePath(sourceDir, file)}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(metadata.Title))
            {
                result.Errors.Add($"Empty title: {Path.GetRelativePath(sourceDir, file)}");
                continue;
            }

            if (metadata.Draft && !includeDrafts)
            {
                result.Warnings.Add($"Skipping draft: {Path.GetRelativePath(sourceDir, file)}");
                continue;
            }

            validFiles.Add((file, metadata, markdown));
        }

        // 5. Build site manifest
        var manifest = SiteManifestBuilder.Build(sourceDir, title, baseUrl);

        // 6. Create LinkResolver with known page paths
        var knownPaths = validFiles
            .Select(f => GetPagePath(f.FilePath, sourceDir))
            .ToList();
        var linkResolver = new LinkResolver(knownPaths);

        // 9. Create output directory
        Directory.CreateDirectory(outputDir);

        // 7. Emit XML for each valid file
        var extensionsList = extensions.ToList();
        foreach (var (filePath, metadata, markdown) in validFiles)
        {
            ct.ThrowIfCancellationRequested();

            var pagePath = GetPagePath(filePath, sourceDir);
            var emitWarnings = new List<string>();
            var xml = MarkdownToXmlEmitter.Emit(markdown, metadata, pagePath, extensionsList,
                linkResolver, emitWarnings);
            result.Warnings.AddRange(emitWarnings);

            // Mirror source structure, .md -> .xml
            var relativePath = Path.GetRelativePath(sourceDir, filePath);
            var outputPath = Path.Combine(outputDir,
                Path.ChangeExtension(relativePath, ".xml"));

            var outputFileDir = Path.GetDirectoryName(outputPath);
            if (outputFileDir != null)
            {
                Directory.CreateDirectory(outputFileDir);
            }

            await File.WriteAllTextAsync(outputPath, xml, ct).ConfigureAwait(false);
        }

        // 8. Write site-manifest.xml
        var manifestXml = SiteManifestBuilder.ToXml(manifest);
        var manifestPath = Path.Combine(outputDir, "site-manifest.xml");
        await Task.Run(() => manifestXml.Save(manifestPath), ct).ConfigureAwait(false);

        // 9. Build search index from the emitted XML
        await SearchIndexBuilder.BuildAsync(outputDir, ct).ConfigureAwait(false);

        timer.Stop();
        result.ParseTiming = timer;

        return result;
    }

    private static string GetPagePath(string filePath, string sourceDir)
    {
        var relative = Path.GetRelativePath(sourceDir, filePath);
        // Remove .md extension
        if (relative.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[..^3];
        }

        return relative.Replace('\\', '/');
    }
}
