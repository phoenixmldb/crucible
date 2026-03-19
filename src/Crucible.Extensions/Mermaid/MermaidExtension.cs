namespace Crucible.Extensions.Mermaid;

using System.Text;
using Crucible.Core.Extensions;
using Markdig.Syntax;

public sealed class MermaidExtension : ICrucibleExtension
{
    public string Name => "Mermaid";

    public bool CanProcess(Type markdigNodeType) =>
        markdigNodeType == typeof(FencedCodeBlock);

    public bool ProcessNode(MarkdownObject node, XmlEmitterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (node is not FencedCodeBlock fenced)
            return false;

        var info = fenced.Info?.Trim();
        if (!string.Equals(info, "mermaid", StringComparison.OrdinalIgnoreCase))
            return false;

        var content = ExtractContent(fenced);
        context.Writer.WriteStartElement("mermaid");
        context.Writer.WriteString(content);
        context.Writer.WriteEndElement();
        return true;
    }

    public IEnumerable<CrucibleAsset> GetAssets()
    {
        var script = Encoding.UTF8.GetBytes("""
            document.addEventListener('DOMContentLoaded', function() {
                if (typeof mermaid !== 'undefined') {
                    mermaid.initialize({ startOnLoad: true, theme: 'default' });
                }
            });
            """);
        yield return new CrucibleAsset("js/mermaid-init.js",
            "application/javascript", new ReadOnlyMemory<byte>(script));
    }

    private static string ExtractContent(FencedCodeBlock block)
    {
        var sb = new StringBuilder();

        if (block.Lines.Count > 0)
        {
            for (var i = 0; i < block.Lines.Count; i++)
            {
                var line = block.Lines.Lines[i];

                if (line.Slice.Length > 0)
                {
                    sb.AppendLine(line.Slice.ToString());
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}
