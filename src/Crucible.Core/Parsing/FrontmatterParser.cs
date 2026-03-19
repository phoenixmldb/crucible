namespace Crucible.Core.Parsing;

using Crucible.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class FrontmatterParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static (DocumentMetadata? Metadata, string Markdown) Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, content);

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, content);

        var yamlBlock = content[3..endIndex].Trim();
        var markdown = content[(endIndex + 4)..].TrimStart('\r', '\n');

        var metadata = YamlDeserializer.Deserialize<DocumentMetadata>(yamlBlock);
        return (metadata, markdown);
    }
}
