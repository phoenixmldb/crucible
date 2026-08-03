namespace SamplePlugin;

using System.Xml;
using Crucible.Core.Extensions;
using Markdig.Syntax;
using SamplePlugin.Support;

/// <summary>
/// Minimal extension that touches its own dependency. Reading <see cref="Name"/> is the
/// first call into <c>SamplePlugin.Support</c>, so an unresolvable dependency surfaces
/// there rather than at load time — matching how this fails for a real plugin.
/// </summary>
public sealed class SamplePluginExtension : ICrucibleExtension
{
    public string Name => Greeter.Describe();

    public bool CanProcess(Type markdigNodeType) => false;

    public bool ProcessNode(MarkdownObject node, XmlEmitterContext context) => false;

    public IEnumerable<CrucibleAsset> GetAssets() => [];
}
