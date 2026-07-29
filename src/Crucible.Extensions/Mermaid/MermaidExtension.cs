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
        // Referenced by the built-in themes via the mermaid-scripts template,
        // which emits it once per page and only when a diagram is present.
        //
        // startOnLoad is deliberately false: this script is included after the
        // mermaid runtime, so auto-start may already have run (or not) depending
        // on load timing. Driving mermaid.run() explicitly is deterministic.
        var script = Encoding.UTF8.GetBytes("""
            (function () {
                function render() {
                    if (typeof mermaid === 'undefined') return;
                    var dark = document.documentElement.getAttribute('data-theme') === 'dark';
                    mermaid.initialize({
                        startOnLoad: false,
                        theme: dark ? 'dark' : 'default'
                    });
                    var nodes = document.querySelectorAll('pre.mermaid');
                    if (!nodes.length) return;
                    if (typeof mermaid.run === 'function') {
                        mermaid.run({ nodes: nodes });
                    } else {
                        mermaid.init(undefined, nodes);
                    }
                }

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', render);
                } else {
                    render();
                }
            })();
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
