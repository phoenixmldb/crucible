namespace Crucible.Core.Themes;

public sealed class ThemeLoader
{
    public string PageXslt { get; }
    public string SitemapXslt { get; }
    public string ThemeDirectory { get; }

    public ThemeLoader(string? customThemePath = null)
    {
        ThemeDirectory = ResolveThemeDirectory(customThemePath);

        var pagePath = Path.Combine(ThemeDirectory, "page.xslt");
        var sitemapPath = Path.Combine(ThemeDirectory, "sitemap.xslt");

        // Fail with the missing file named, rather than letting ReadAllText
        // surface a bare path from somewhere inside the call stack.
        foreach (var required in new[] { pagePath, sitemapPath })
        {
            if (!File.Exists(required))
            {
                throw new FileNotFoundException(
                    $"'{ThemeDirectory}' is not a theme: {Path.GetFileName(required)} is missing.",
                    required);
            }
        }

        PageXslt = File.ReadAllText(pagePath);
        SitemapXslt = File.ReadAllText(sitemapPath);
    }

    public IEnumerable<(string RelativePath, string FullPath)> GetStaticAssets()
    {
        var cssDir = Path.Combine(ThemeDirectory, "css");
        var jsDir = Path.Combine(ThemeDirectory, "js");
        foreach (var dir in new[] { cssDir, jsDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                yield return (Path.GetRelativePath(ThemeDirectory, file), file);
            }
        }
    }

    private static string ResolveThemeDirectory(string? customThemePath)
    {
        if (string.IsNullOrEmpty(customThemePath))
        {
            return GetBuiltInPath("default");
        }

        if (Directory.Exists(customThemePath))
        {
            return customThemePath;
        }

        if (IsBuiltInName(customThemePath))
        {
            var builtIn = GetBuiltInPath(customThemePath);
            if (Directory.Exists(builtIn))
            {
                return builtIn;
            }
        }

        throw new DirectoryNotFoundException(
            $"Theme '{customThemePath}' not found as a directory or a built-in theme.");
    }

    /// <summary>
    /// A built-in theme is a single directory name directly under Themes/.
    /// Anything containing a separator, a traversal segment, or a leading
    /// underscore (reserved for shared stylesheet fragments such as _base) is
    /// not a theme name and must be supplied as an explicit directory instead.
    /// </summary>
    private static bool IsBuiltInName(string name) =>
        name.Length > 0
        && name[0] != '_'
        && name.AsSpan().IndexOfAny('/', '\\') < 0
        && !name.Contains("..", StringComparison.Ordinal)
        && name.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string GetBuiltInPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Themes", name);
}
