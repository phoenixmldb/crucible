#pragma warning disable CA1305 // String interpolation in llms.txt output is not locale-dependent

namespace Crucible.Core.Search;

using System.Globalization;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Generates llms.txt and llms-full.txt files for AI model consumption.
/// llms.txt provides a structured overview of the site.
/// llms-full.txt includes the full text content of every page.
/// </summary>
public static class LlmsTxtGenerator
{
    /// <summary>
    /// Generates llms.txt and llms-full.txt from intermediate XML documents.
    /// </summary>
    public static async Task GenerateAsync(string xmlDirectory, string outputDirectory,
        string siteTitle, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(xmlDirectory, "site-manifest.xml");
        if (!File.Exists(manifestPath))
            return;

        using var manifestReader = new StreamReader(manifestPath);
        var manifest = XDocument.Load(manifestReader);
        var baseUrl = manifest.Root?.Attribute("base-url")?.Value ?? "/";

        var xmlFiles = Directory.GetFiles(xmlDirectory, "*.xml", SearchOption.AllDirectories);
        var documents = new List<(string Path, string Title, string Description, string Body)>();

        foreach (var xmlFile in xmlFiles)
        {
            ct.ThrowIfCancellationRequested();

            if (System.IO.Path.GetFileName(xmlFile)
                .Equals("site-manifest.xml", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var reader = new StreamReader(xmlFile);
                var doc = XDocument.Load(reader);
                var root = doc.Root;
                if (root?.Name.LocalName != "document")
                    continue;

                var path = root.Attribute("path")?.Value ?? "";
                var title = root.Attribute("title")?.Value ?? "";
                var description = root.Attribute("description")?.Value ?? "";
                var body = ExtractText(root.Element("body"));

                documents.Add((path, title, description, body));
            }
#pragma warning disable CA1031
            catch { /* skip malformed files */ }
#pragma warning restore CA1031
        }

        // Sort by path for consistent output
        documents.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

        // Generate llms.txt — structured overview
        var llmsTxt = new StringBuilder();
        llmsTxt.AppendLine($"# {siteTitle}");
        llmsTxt.AppendLine();
        llmsTxt.AppendLine($"> {siteTitle} — documentation site");
        llmsTxt.AppendLine();

        // Group by top-level section
        var sections = documents
            .GroupBy(d => d.Path.Contains('/', StringComparison.Ordinal)
                ? d.Path[..d.Path.IndexOf('/', StringComparison.Ordinal)]
                : "")
            .OrderBy(g => g.Key);

        foreach (var section in sections)
        {
            if (!string.IsNullOrEmpty(section.Key))
            {
                llmsTxt.AppendLine($"## {section.Key}");
                llmsTxt.AppendLine();
            }

            foreach (var doc in section)
            {
                llmsTxt.Append($"- [{doc.Title}]({baseUrl}{doc.Path}.html)");
                if (!string.IsNullOrEmpty(doc.Description))
                    llmsTxt.Append($": {doc.Description}");
                llmsTxt.AppendLine();
            }

            llmsTxt.AppendLine();
        }

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(outputDirectory, "llms.txt"),
            llmsTxt.ToString(), ct).ConfigureAwait(false);

        // Generate llms-full.txt — complete content
        var fullTxt = new StringBuilder();
        fullTxt.AppendLine($"# {siteTitle}");
        fullTxt.AppendLine();

        foreach (var doc in documents)
        {
            ct.ThrowIfCancellationRequested();

            fullTxt.AppendLine($"## {doc.Title}");
            if (!string.IsNullOrEmpty(doc.Description))
                fullTxt.AppendLine($"> {doc.Description}");
            fullTxt.AppendLine($"URL: {baseUrl}{doc.Path}.html");
            fullTxt.AppendLine();
            fullTxt.AppendLine(doc.Body);
            fullTxt.AppendLine();
            fullTxt.AppendLine("---");
            fullTxt.AppendLine();
        }

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(outputDirectory, "llms-full.txt"),
            fullTxt.ToString(), ct).ConfigureAwait(false);
    }

    private static string ExtractText(XElement? element)
    {
        if (element == null) return "";

        var sb = new StringBuilder();
        foreach (var node in element.DescendantNodes())
        {
            if (node is XText text)
            {
                sb.Append(text.Value);
                sb.Append(' ');
            }
        }

        return sb.ToString().Trim();
    }
}
