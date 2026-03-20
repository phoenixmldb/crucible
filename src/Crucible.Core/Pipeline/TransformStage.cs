namespace Crucible.Core.Pipeline;

using System.Diagnostics;
using System.Xml.Linq;
using Crucible.Core.Extensions;
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

        // Transform all pages, reusing a single compiled stylesheet
        foreach (var xmlFile in pageFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(inputDir, xmlFile);
            var outputRelativePath = Path.ChangeExtension(relativePath, ".html");
            var outputPath = Path.Combine(outputDir, outputRelativePath);
            var currentPath = Path.ChangeExtension(relativePath, null).Replace('\\', '/');

            try
            {
                // Create a new transformer per page (XsltTransformer holds per-transform state
                // like parameters and secondary documents, so it can't be fully reused).
                // But the stylesheet compilation is the expensive part — if the engine caches
                // compiled stylesheets internally, this is fast.
                var transformer = new XsltTransformer();
                await transformer.LoadStylesheetAsync(theme.PageXslt, pageXsltBaseUri).ConfigureAwait(true);
                transformer.SetParameter("site-manifest-uri", manifestUri);
                transformer.SetParameter("base-url", baseUrl);
                transformer.SetParameter("site-title", siteTitle);
                transformer.SetParameter("current-path", currentPath);

                var documentXml = await File.ReadAllTextAsync(xmlFile, ct).ConfigureAwait(true);
                var html = await transformer.TransformAsync(documentXml, ct).ConfigureAwait(true);

                var outputFileDir = Path.GetDirectoryName(outputPath);
                if (outputFileDir != null)
                {
                    Directory.CreateDirectory(outputFileDir);
                }

                await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(true);
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
            await sitemapTransformer.LoadStylesheetAsync(theme.SitemapXslt, sitemapXsltBaseUri).ConfigureAwait(true);

            var manifestXml = await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(true);
            var sitemapXml = await sitemapTransformer.TransformAsync(manifestXml, ct).ConfigureAwait(true);

            await File.WriteAllTextAsync(Path.Combine(outputDir, "sitemap.xml"), sitemapXml, ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Errors.Add($"Sitemap generation failed: {ex.Message}");
        }

        // 6. Generate llms.txt and llms-full.txt
        await LlmsTxtGenerator.GenerateAsync(inputDir, outputDir, siteTitle, ct)
            .ConfigureAwait(true);

        // 7. Copy search index if it exists
        var searchIndexPath = Path.Combine(inputDir, "search-index.json");
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

                await File.WriteAllBytesAsync(destPath, asset.Content.ToArray(), ct).ConfigureAwait(true);
            }
        }

        timer.Stop();
        result.TransformTiming = timer;

        return result;
    }
}
