namespace Crucible.Core.Parsing;

using Crucible.Core.Urls;

public sealed class LinkResolver
{
    private readonly HashSet<string> _knownPaths;

    public LinkResolver(IEnumerable<string> knownPaths)
    {
        _knownPaths = new HashSet<string>(knownPaths, StringComparer.OrdinalIgnoreCase);
    }

    public LinkResult Resolve(string href, string currentPath)
    {
        ArgumentNullException.ThrowIfNull(href);
        ArgumentNullException.ThrowIfNull(currentPath);

        // External links pass through
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return new LinkResult(href, IsBroken: false);
        }

        // Split fragment
        string? fragment = null;
        var fragmentIdx = href.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIdx >= 0)
        {
            fragment = href[fragmentIdx..];
            href = href[..fragmentIdx];
        }

        // Not a .md link — pass through
        if (!href.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return new LinkResult(href + (fragment ?? ""), IsBroken: false);
        }

        // Resolve the .md link to the URL the rest of the pipeline uses for that
        // page. This used to append ".html", which every clean-URL host answers
        // with a 308 to the form below -- see PageUrl for what that cost.
        var urlHref = PageUrl.CollapseIndex(href[..^3]);

        // Collapsing a bare "index.md" leaves nothing, and an empty href means
        // "this page". "./" is the directory the collapse was aiming at anyway.
        if (urlHref.Length == 0)
            urlHref = "./";

        // Check if target exists
        var targetPath = ResolvePath(href[..^3], currentPath, href.StartsWith('/'));
        var isBroken = targetPath != null && !_knownPaths.Contains(targetPath);

        return new LinkResult(urlHref + (fragment ?? ""), isBroken);
    }

    private static string? ResolvePath(string target, string currentPath, bool isRootRelative)
    {
        if (isRootRelative)
        {
            return target.TrimStart('/');
        }

        var lastSlash = currentPath.LastIndexOf('/');
        var currentDir = lastSlash >= 0
            ? currentPath[..lastSlash]
            : "";

        var combined = string.IsNullOrEmpty(currentDir)
            ? target
            : $"{currentDir}/{target}";

        // Normalize . and ..
        var segments = combined.Split('/');
        var result = new List<string>();
        foreach (var seg in segments)
        {
            if (seg == ".")
            {
                continue;
            }

            if (seg == ".." && result.Count > 0)
            {
                result.RemoveAt(result.Count - 1);
                continue;
            }

            result.Add(seg);
        }

        return string.Join("/", result);
    }
}

public record LinkResult(string ResolvedHref, bool IsBroken);
