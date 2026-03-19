namespace Crucible.Core.Manifest;

using System.Globalization;
using System.Xml.Linq;
using Crucible.Core.Models;
using Crucible.Core.Parsing;

#pragma warning disable CA1054 // URI parameters should not be strings — baseUrl is a path prefix, not a full URI

public static class SiteManifestBuilder
{
    public static SiteManifest Build(string sourceDir, string title, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(sourceDir);

        var manifest = new SiteManifest
        {
            Title = title,
            BaseUrl = baseUrl,
            Children = BuildChildren(sourceDir, sourceDir),
        };

        return manifest;
    }

    public static XDocument ToXml(SiteManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var root = new XElement("site",
            new XAttribute("title", manifest.Title),
            new XAttribute("base-url", manifest.BaseUrl));

        foreach (var child in manifest.Children)
        {
            root.Add(NodeToXml(child));
        }

        return new XDocument(root);
    }

    private static List<ISiteNode> BuildChildren(string directory, string rootDir)
    {
        var children = new List<ISiteNode>();

        // Process .md files in this directory (excluding index.md which is used for section metadata)
        var mdFiles = Directory.GetFiles(directory, "*.md")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in mdFiles)
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "index.md", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(directory, rootDir, StringComparison.Ordinal))
            {
                // index.md in subdirectories defines the section, not a separate page
                continue;
            }

            var content = File.ReadAllText(file);
            var (metadata, _) = FrontmatterParser.Parse(content);
            var relativePath = GetRelativePath(file, rootDir);

            children.Add(new SitePage
            {
                Path = relativePath,
                Title = metadata?.Title ?? TitleCase(Path.GetFileNameWithoutExtension(file)),
                Sort = metadata?.Sort,
                Description = metadata?.Description,
                Updated = metadata?.Updated,
            });
        }

        // Process subdirectories as sections
        var subdirs = Directory.GetDirectories(directory)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var subdir in subdirs)
        {
            var dirName = Path.GetFileName(subdir);
            var sectionTitle = dirName ?? "";
            int? sectionSort = null;

            // Check for index.md to get section metadata
            var indexFile = Path.Combine(subdir, "index.md");
            if (File.Exists(indexFile))
            {
                var content = File.ReadAllText(indexFile);
                var (metadata, _) = FrontmatterParser.Parse(content);
                if (metadata != null && !string.IsNullOrEmpty(metadata.Title))
                {
                    sectionTitle = metadata.Title;
                }
                else
                {
                    sectionTitle = TitleCase(dirName ?? "");
                }

                sectionSort = metadata?.Sort;
            }
            else
            {
                sectionTitle = TitleCase(dirName ?? "");
            }

            var relativePath = GetRelativePath(subdir, rootDir);
            var sectionChildren = BuildChildren(subdir, rootDir);

            children.Add(new SiteSection
            {
                Path = relativePath,
                Title = sectionTitle,
                Sort = sectionSort,
                Children = sectionChildren,
            });
        }

        // Sort: pages with explicit sort values first (ascending), then unsorted alphabetically by path
        children.Sort((a, b) =>
        {
            var aSort = a.Sort;
            var bSort = b.Sort;

            if (aSort.HasValue && bSort.HasValue)
            {
                var cmp = aSort.Value.CompareTo(bSort.Value);
                return cmp != 0 ? cmp : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            }

            if (aSort.HasValue)
                return -1;
            if (bSort.HasValue)
                return 1;

            return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        });

        return children;
    }

    private static string GetRelativePath(string fullPath, string rootDir)
    {
        var relative = System.IO.Path.GetRelativePath(rootDir, fullPath);
        // Remove .md extension for pages
        if (relative.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[..^3];
        }

        // Normalize to forward slashes
        return relative.Replace('\\', '/');
    }

    private static string TitleCase(string name)
    {
        // Convert kebab-case or snake_case directory names to Title Case
        var words = name.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w =>
            char.ToUpper(w[0], CultureInfo.InvariantCulture) + w[1..]));
    }

    private static XElement NodeToXml(ISiteNode node)
    {
        if (node is SiteSection section)
        {
            var element = new XElement("section",
                new XAttribute("path", section.Path),
                new XAttribute("title", section.Title));

            if (section.Sort.HasValue)
            {
                element.Add(new XAttribute("sort", section.Sort.Value));
            }

            foreach (var child in section.Children)
            {
                element.Add(NodeToXml(child));
            }

            return element;
        }

        var page = (SitePage)node;
        var pageElement = new XElement("page",
            new XAttribute("path", page.Path),
            new XAttribute("title", page.Title));

        if (page.Sort.HasValue)
        {
            pageElement.Add(new XAttribute("sort", page.Sort.Value));
        }

        if (page.Updated.HasValue)
        {
            pageElement.Add(new XAttribute("updated",
                page.Updated.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return pageElement;
    }
}
