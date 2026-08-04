namespace Crucible.Core.Pipeline;

using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Crucible.Core.Extensions;
using Crucible.Core.Models;
using Crucible.Core.Search;
using Crucible.Core.Themes;
using PhoenixmlDb.Xslt;

public static class TransformStage
{
    public static async Task<BuildResult> ExecuteAsync(
        string inputDir,
        string outputDir,
        string? themePath,
        IEnumerable<ICrucibleExtension> extensions,
        AnalyticsConfig? analytics = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputDir);
        ArgumentNullException.ThrowIfNull(outputDir);
        ArgumentNullException.ThrowIfNull(extensions);

        var result = new BuildResult();
        var timer = Stopwatch.StartNew();

        // 1. Load theme
        var theme = new ThemeLoader(themePath);

        // 2. Read site-manifest.xml
        var manifestPath = Path.Combine(inputDir, "site-manifest.xml");
        if (!File.Exists(manifestPath))
        {
            result.Errors.Add("site-manifest.xml not found in input directory");
            return result;
        }

        var manifestDoc = XDocument.Load(manifestPath);
        var siteElement = manifestDoc.Root;
        var baseUrl = siteElement?.Attribute("base-url")?.Value ?? "/";
        var siteTitle = siteElement?.Attribute("title")?.Value ?? "Documentation";
        var manifestUri = new Uri(Path.GetFullPath(manifestPath)).AbsoluteUri;

        // Resolve the base URI for stylesheets (for xsl:import resolution)
        var pageXsltPath = Path.Combine(theme.ThemeDirectory, "page.xslt");
        var pageXsltBaseUri = new Uri(Path.GetFullPath(pageXsltPath));
        var sitemapXsltPath = Path.Combine(theme.ThemeDirectory, "sitemap.xslt");
        var sitemapXsltBaseUri = new Uri(Path.GetFullPath(sitemapXsltPath));

        // 3. Create output directory
        Directory.CreateDirectory(outputDir);

        // 4. Compile the page stylesheet ONCE and reuse for all pages
        var xmlFiles = Directory.GetFiles(inputDir, "*.xml", SearchOption.AllDirectories);
        var pageFiles = new List<string>();
        foreach (var xmlFile in xmlFiles)
        {
            var fileName = Path.GetFileName(xmlFile);
            if (!string.Equals(fileName, "site-manifest.xml", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fileName, "search-index.json", StringComparison.OrdinalIgnoreCase))
            {
                pageFiles.Add(xmlFile);
            }
        }

        // Compile the page stylesheet once. Measured on a 100-page site with the
        // modern theme: ~4830ms re-loading per page vs ~2970ms compiling once
        // (~19ms/page). Every parameter the stylesheet reads is re-set on every
        // iteration below, so no per-page value can leak into the next page.
        //
        // The transformer is reused only because the built-in themes produce a
        // single result tree. A theme using xsl:result-document would accumulate
        // entries in SecondaryResultDocuments across pages, so if that is ever
        // supported this must go back to one transformer per page.
        var pageTransformer = new XsltTransformer();
        await pageTransformer.LoadStylesheetAsync(theme.PageXslt, pageXsltBaseUri).ConfigureAwait(false);

        foreach (var xmlFile in pageFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(inputDir, xmlFile);
            var outputRelativePath = Path.ChangeExtension(relativePath, ".html");
            var outputPath = Path.Combine(outputDir, outputRelativePath);
            var currentPath = Path.ChangeExtension(relativePath, null).Replace('\\', '/');

            try
            {
                var transformer = pageTransformer;
                transformer.SetParameter("site-manifest-uri", manifestUri);
                transformer.SetParameter("base-url", baseUrl);
                transformer.SetParameter("site-title", siteTitle);
                transformer.SetParameter("current-path", currentPath);
                transformer.SetParameter("ga4-id", analytics?.Ga4 ?? "");

                var documentXml = await File.ReadAllTextAsync(xmlFile, ct).ConfigureAwait(false);
                var html = await transformer.TransformAsync(documentXml, ct).ConfigureAwait(false);

                var outputFileDir = Path.GetDirectoryName(outputPath);
                if (outputFileDir != null)
                {
                    Directory.CreateDirectory(outputFileDir);
                }

                await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Errors.Add($"Transform failed for {relativePath}: {ex.Message}");
            }
        }

        // 5. Generate sitemap.xml
        try
        {
            var sitemapTransformer = new XsltTransformer();
            await sitemapTransformer.LoadStylesheetAsync(theme.SitemapXslt, sitemapXsltBaseUri).ConfigureAwait(false);

            var manifestXml = await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false);
            var sitemapXml = await sitemapTransformer.TransformAsync(manifestXml, ct).ConfigureAwait(false);

            await File.WriteAllTextAsync(Path.Combine(outputDir, "sitemap.xml"), sitemapXml, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Errors.Add($"Sitemap generation failed: {ex.Message}");
        }

        // 6. Generate llms.txt and llms-full.txt
        await LlmsTxtGenerator.GenerateAsync(inputDir, outputDir, siteTitle, result.Warnings, ct)
            .ConfigureAwait(false);

        // 7. Search index. ParseStage builds one, but TransformOnly runs against a
        // directory the caller may have added documents to after parsing (the
        // documented extension point — e.g. generated API reference pages). A
        // parse-time index would silently omit those, so rebuild whenever the index
        // no longer covers what is on disk — see IsSearchIndexStale for why that is
        // a coverage question rather than a timestamp one.
        var searchIndexPath = Path.Combine(inputDir, "search-index.json");
        if (IsSearchIndexStale(inputDir, searchIndexPath))
        {
            await SearchIndexBuilder.BuildAsync(inputDir, result.Warnings, ct).ConfigureAwait(false);
        }

        if (File.Exists(searchIndexPath))
        {
            File.Copy(searchIndexPath, Path.Combine(outputDir, "search-index.json"), overwrite: true);
        }

        // 8. Copy theme static assets
        foreach (var (relativePath, fullPath) in theme.GetStaticAssets())
        {
            ct.ThrowIfCancellationRequested();

            var destPath = Path.Combine(outputDir, relativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir != null)
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(fullPath, destPath, overwrite: true);
        }

        // 9. Copy extension assets
        var extensionsList = extensions.ToList();
        foreach (var extension in extensionsList)
        {
            foreach (var asset in extension.GetAssets())
            {
                ct.ThrowIfCancellationRequested();

                var destPath = Path.Combine(outputDir, asset.RelativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (destDir != null)
                {
                    Directory.CreateDirectory(destDir);
                }

                await File.WriteAllBytesAsync(destPath, asset.Content.ToArray(), ct).ConfigureAwait(false);
            }
        }

        timer.Stop();
        result.TransformTiming = timer;

        return result;
    }

    /// <summary>
    /// True when the search index does not cover the documents currently on disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Coverage is checked by count before it is checked by time, because modification
    /// times answer the wrong question. A document injected after parsing — the documented
    /// extension point, e.g. generated API reference pages — can easily land inside the
    /// index's timestamp granularity, and a file copied in with its time preserved is
    /// <i>older</i> than the index that predates it. Both cases ship an index missing real
    /// pages, and neither is visible to a comparison of write times.
    /// </para>
    /// <para>
    /// Ties count as stale. Rebuilding unnecessarily costs one pass over already-parsed
    /// XML; failing to rebuild ships a site whose search cannot find pages that are on it.
    /// The same bias explains why an unreadable index is treated as stale rather than
    /// trusted.
    /// </para>
    /// </remarks>
    private static bool IsSearchIndexStale(string inputDir, string searchIndexPath)
    {
        if (!File.Exists(searchIndexPath))
        {
            return true;
        }

        var documents = Directory.EnumerateFiles(inputDir, "*.xml", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFileName(f), "site-manifest.xml",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (CountIndexedDocuments(searchIndexPath) != documents.Count)
        {
            return true;
        }

        var indexWritten = File.GetLastWriteTimeUtc(searchIndexPath);
        return documents.Exists(f => File.GetLastWriteTimeUtc(f) >= indexWritten);
    }

    /// <summary>
    /// Number of entries in an existing index, or -1 if it cannot be read — which forces
    /// a rebuild rather than trusting a file we could not parse.
    /// </summary>
    private static int CountIndexedDocuments(string searchIndexPath)
    {
        try
        {
            using var stream = File.OpenRead(searchIndexPath);
            using var json = JsonDocument.Parse(stream);
            return json.RootElement.ValueKind == JsonValueKind.Array
                ? json.RootElement.GetArrayLength()
                : -1;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return -1;
        }
    }
}
