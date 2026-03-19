namespace Crucible.Core.Extensions;

using System.Xml;
using Crucible.Core.Models;
using Markdig.Syntax;

public interface ICrucibleExtension
{
    string Name { get; }
    bool CanProcess(Type markdigNodeType);
    bool ProcessNode(MarkdownObject node, XmlEmitterContext context);
    IEnumerable<CrucibleAsset> GetAssets();
}

public sealed class XmlEmitterContext
{
    public required XmlWriter Writer { get; init; }
    public required string DocumentPath { get; init; }
    public SiteManifest? Manifest { get; init; }
}

public record CrucibleAsset(string RelativePath, string ContentType, ReadOnlyMemory<byte> Content);
