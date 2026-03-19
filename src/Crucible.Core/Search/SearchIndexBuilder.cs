namespace Crucible.Core.Search;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Builds a search index JSON file from document XML files for client-side search with Lunr.js.
/// </summary>
public static partial class SearchIndexBuilder
{
    /// <summary>
    /// Builds a search index from the intermediate XML documents in the output directory.
    /// Writes search-index.json to the same directory.
    /// </summary>
    public static async Task BuildAsync(string xmlDirectory, CancellationToken ct = default)
    {
        var documents = new List<SearchDocument>();
        var xmlFiles = Directory.GetFiles(xmlDirectory, "*.xml", SearchOption.AllDirectories);

        foreach (var xmlFile in xmlFiles)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(xmlFile);
            if (string.Equals(fileName, "site-manifest.xml", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                // Load with StreamReader to avoid encoding mismatch
                // (XmlWriter writes encoding="utf-16" in declaration when using StringWriter,
                // but File.WriteAllText writes UTF-8)
                using var reader = new StreamReader(xmlFile);
                var doc = XDocument.Load(reader);
                var root = doc.Root;
                if (root?.Name.LocalName != "document")
                    continue;

                var path = root.Attribute("path")?.Value ?? "";
                var title = root.Attribute("title")?.Value ?? "";
                var description = root.Attribute("description")?.Value ?? "";

                // Extract headings for section-level search
                var headings = root.Descendants("heading")
                    .Select(h => StripXml(h))
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .ToList();

                // Extract body text (strip XML tags, normalize whitespace)
                var body = root.Element("body");
                var bodyText = body != null ? StripXml(body) : "";

                // Truncate body to keep index size reasonable
                if (bodyText.Length > 2000)
                    bodyText = bodyText[..2000];

                documents.Add(new SearchDocument(
                    path,
                    title,
                    description,
                    headings,
                    bodyText));
            }
#pragma warning disable CA1031 // Catch general exception — skip malformed files
            catch
#pragma warning restore CA1031
            {
                // Skip files that can't be parsed
            }
        }

        var json = JsonSerializer.Serialize(documents, JsonContext.Default.ListSearchDocument);
        var indexPath = Path.Combine(xmlDirectory, "search-index.json");
        await File.WriteAllTextAsync(indexPath, json, ct).ConfigureAwait(false);
    }

    private static string StripXml(XElement element)
    {
        var sb = new StringBuilder();
        foreach (var node in element.DescendantNodes())
        {
            if (node is XText text)
            {
                sb.Append(text.Value);
                sb.Append(' ');
            }
        }

        return CollapseWhitespace().Replace(sb.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();
}

public record SearchDocument(
    string Path,
    string Title,
    string Description,
    IReadOnlyList<string> Headings,
    string Body);

[System.Text.Json.Serialization.JsonSerializable(typeof(List<SearchDocument>))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal sealed partial class JsonContext : JsonSerializerContext;
