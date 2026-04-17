namespace Crucible.Core.Themes;

public sealed class ThemeLoader
{
    public string PageXslt { get; }
    public string SitemapXslt { get; }
    public string ThemeDirectory { get; }

    public ThemeLoader(string? customThemePath = null)
    {
        ThemeDirectory = ResolveThemeDirectory(customThemePath);
        PageXslt = File.ReadAllText(Path.Combine(ThemeDirectory, "page.xslt"));
        SitemapXslt = File.ReadAllText(Path.Combine(ThemeDirectory, "sitemap.xslt"));
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

        var builtIn = GetBuiltInPath(customThemePath);
        if (Directory.Exists(builtIn))
        {
            return builtIn;
        }

        throw new DirectoryNotFoundException(
            $"Theme '{customThemePath}' not found as a directory or a built-in theme.");
    }

    private static string GetBuiltInPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Themes", name);
}
