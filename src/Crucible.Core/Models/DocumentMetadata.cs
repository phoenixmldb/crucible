namespace Crucible.Core.Models;

#pragma warning disable CA1002 // Do not expose generic lists — DTO used for YAML deserialization
#pragma warning disable CA2227 // Collection properties should be read only — DTO used for YAML deserialization

public sealed class DocumentMetadata
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? Sort { get; set; }
    public DateTime? Updated { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool Draft { get; set; }
    public string? Template { get; set; }

    [YamlDotNet.Serialization.YamlIgnore]
    public Dictionary<string, object?> Extra { get; set; } = [];
}
