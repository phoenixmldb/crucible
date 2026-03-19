namespace Crucible.Extensions;

using Crucible.Core.Extensions;
using Crucible.Extensions.Mermaid;

#pragma warning disable CA1002 // Do not expose generic lists — simple factory method for extension registration

public static class ExtensionRegistry
{
    public static List<ICrucibleExtension> DefaultExtensions =>
        [new MermaidExtension()];
}
