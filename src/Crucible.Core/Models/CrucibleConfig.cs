namespace Crucible.Core.Models;

#pragma warning disable CA1002 // Do not expose generic lists — DTO used for YAML deserialization
#pragma warning disable CA1056 // URI properties should not be strings — BaseUrl is a path prefix, not a full URI
#pragma warning disable CA2227 // Collection properties should be read only — DTO used for YAML deserialization

public sealed class CrucibleConfig
{
    public string Title { get; set; } = "My Documentation";

    [YamlDotNet.Serialization.YamlMember(Alias = "base-url")]
    public string BaseUrl { get; set; } = "/";

    public string Source { get; set; } = "./docs";
    public string Output { get; set; } = "./dist";
    public string? Theme { get; set; }
    public List<string> Extensions { get; set; } = [];
}
