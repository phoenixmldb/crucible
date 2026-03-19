namespace Crucible.Core.Models;

#pragma warning disable CA1002 // Do not expose generic lists — model used for tree construction
#pragma warning disable CA1056 // URI properties should not be strings — BaseUrl is a path prefix, not a full URI

public sealed class SiteManifest
{
    public required string Title { get; init; }
    public required string BaseUrl { get; init; }
    public List<ISiteNode> Children { get; init; } = [];
}

public interface ISiteNode
{
    string Path { get; }
    string Title { get; }
    int? Sort { get; }
}

public sealed class SitePage : ISiteNode
{
    public required string Path { get; init; }
    public required string Title { get; init; }
    public int? Sort { get; init; }
    public string? Description { get; init; }
    public DateTime? Updated { get; init; }
}

public sealed class SiteSection : ISiteNode
{
    public required string Path { get; init; }
    public required string Title { get; init; }
    public int? Sort { get; init; }
    public List<ISiteNode> Children { get; init; } = [];
}
