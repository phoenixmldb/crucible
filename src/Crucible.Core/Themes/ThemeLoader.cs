namespace Crucible.Core.Themes;

public sealed class ThemeLoader
{
    public string PageXslt { get; }
    public string SitemapXslt { get; }
    public string ThemeDirectory { get; }

    public ThemeLoader(string? customThemePath = null)
    {
        ThemeDirectory = customThemePath ?? GetDefaultThemePath();
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

    private static string GetDefaultThemePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Themes", "default");
    }
}
