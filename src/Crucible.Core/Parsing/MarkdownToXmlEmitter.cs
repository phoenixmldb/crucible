namespace Crucible.Core.Parsing;

using System.Globalization;
using System.Text;
using System.Xml;
using Crucible.Core.Extensions;
using Crucible.Core.Models;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

public static class MarkdownToXmlEmitter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .UseCustomContainers()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .Build();

    public static string Emit(string markdown, DocumentMetadata metadata,
        string path, IEnumerable<ICrucibleExtension>? extensions = null,
        LinkResolver? linkResolver = null, ICollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var dispatch = BuildDispatchTable(extensions);

        var doc = Markdown.Parse(markdown, Pipeline);
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
        };

        using var sw = new StringWriter();
        using var writer = XmlWriter.Create(sw, settings);
        var context = new XmlEmitterContext
        {
            Writer = writer,
            DocumentPath = path,
            LinkResolver = linkResolver,
            Warnings = warnings ?? [],
        };

        writer.WriteStartDocument();
        writer.WriteStartElement("document");
        writer.WriteAttributeString("path", path);
        writer.WriteAttributeString("title", metadata.Title);

        if (metadata.Description != null)
        {
            writer.WriteAttributeString("description", metadata.Description);
        }

        if (metadata.Updated.HasValue)
        {
            writer.WriteAttributeString("updated",
                metadata.Updated.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (metadata.Tags.Count > 0 || metadata.Extra.Count > 0)
        {
            writer.WriteStartElement("meta");

            foreach (var tag in metadata.Tags)
            {
                writer.WriteElementString("tag", tag);
            }

            foreach (var kvp in metadata.Extra)
            {
                writer.WriteElementString(kvp.Key, kvp.Value?.ToString() ?? "");
            }

            writer.WriteEndElement();
        }

        writer.WriteStartElement("body");

        foreach (var block in doc)
        {
            EmitBlock(block, context, dispatch);
        }

        writer.WriteEndElement(); // body
        writer.WriteEndElement(); // document
        writer.WriteEndDocument();
        writer.Flush();
        return sw.ToString();
    }

    private static Dictionary<Type, ICrucibleExtension> BuildDispatchTable(
        IEnumerable<ICrucibleExtension>? extensions)
    {
        var dispatch = new Dictionary<Type, ICrucibleExtension>();

        if (extensions == null)
        {
            return dispatch;
        }

        foreach (var ext in extensions)
        {
            foreach (var type in AllMarkdigNodeTypes)
            {
                if (ext.CanProcess(type))
                {
                    dispatch[type] = ext;
                }
            }
        }

        return dispatch;
    }

    private static readonly Type[] AllMarkdigNodeTypes =
    [
        typeof(HeadingBlock),
        typeof(ParagraphBlock),
        typeof(FencedCodeBlock),
        typeof(CodeBlock),
        typeof(ListBlock),
        typeof(QuoteBlock),
        typeof(Table),
        typeof(ThematicBreakBlock),
        typeof(CustomContainer),
    ];

    private static void EmitBlock(Block block, XmlEmitterContext ctx,
        Dictionary<Type, ICrucibleExtension> dispatch)
    {
        if (dispatch.TryGetValue(block.GetType(), out var ext) && ext.ProcessNode(block, ctx))
        {
            return;
        }

        switch (block)
        {
            case HeadingBlock heading:
                EmitHeading(heading, ctx);
                break;
            case ParagraphBlock para:
                ctx.Writer.WriteStartElement("paragraph");
                EmitInlines(para.Inline, ctx);
                ctx.Writer.WriteEndElement();
                break;
            case FencedCodeBlock fenced:
                EmitFencedCodeBlock(fenced, ctx);
                break;
            case CodeBlock code:
                ctx.Writer.WriteStartElement("code-block");
                ctx.Writer.WriteString(ExtractLines(code));
                ctx.Writer.WriteEndElement();
                break;
            case ListBlock list:
                EmitList(list, ctx, dispatch);
                break;
            case QuoteBlock quote:
                ctx.Writer.WriteStartElement("blockquote");
                foreach (var child in quote)
                {
                    EmitBlock(child, ctx, dispatch);
                }

                ctx.Writer.WriteEndElement();
                break;
            case Table table:
                EmitTable(table, ctx, dispatch);
                break;
            case ThematicBreakBlock:
                ctx.Writer.WriteStartElement("thematic-break");
                ctx.Writer.WriteEndElement();
                break;
            case CustomContainer container:
                EmitAdmonition(container, ctx, dispatch);
                break;
            default:
                if (block is ContainerBlock containerBlock)
                {
                    foreach (var child in containerBlock)
                    {
                        EmitBlock(child, ctx, dispatch);
                    }
                }

                break;
        }
    }

    private static void EmitHeading(HeadingBlock heading, XmlEmitterContext ctx)
    {
        ctx.Writer.WriteStartElement("heading");
        ctx.Writer.WriteAttributeString("level",
            heading.Level.ToString(CultureInfo.InvariantCulture));

        var headingText = GetPlainText(heading.Inline);
        ctx.Writer.WriteAttributeString("id", SlugGenerator.Generate(headingText));
        EmitInlines(heading.Inline, ctx);
        ctx.Writer.WriteEndElement();
    }

    private static void EmitFencedCodeBlock(FencedCodeBlock fenced, XmlEmitterContext ctx)
    {
        ctx.Writer.WriteStartElement("code-block");

        if (!string.IsNullOrEmpty(fenced.Info))
        {
            ctx.Writer.WriteAttributeString("language", fenced.Info);
        }

        ctx.Writer.WriteString(ExtractLines(fenced));
        ctx.Writer.WriteEndElement();
    }

    private static void EmitList(ListBlock list, XmlEmitterContext ctx,
        Dictionary<Type, ICrucibleExtension> dispatch)
    {
        ctx.Writer.WriteStartElement("list");
        ctx.Writer.WriteAttributeString("type",
            list.IsOrdered ? "ordered" : "unordered");

        foreach (var item in list)
        {
            ctx.Writer.WriteStartElement("item");

            if (item is ListItemBlock listItem)
            {
                foreach (var child in listItem)
                {
                    EmitBlock(child, ctx, dispatch);
                }
            }

            ctx.Writer.WriteEndElement();
        }

        ctx.Writer.WriteEndElement();
    }

    private static void EmitInlines(ContainerInline? container, XmlEmitterContext ctx)
    {
        if (container == null)
        {
            return;
        }

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    ctx.Writer.WriteString(literal.Content.ToString());
                    break;
                case EmphasisInline emphasis:
                    var elem = emphasis.DelimiterCount >= 2 ? "strong" : "emphasis";
                    ctx.Writer.WriteStartElement(elem);
                    EmitInlines(emphasis, ctx);
                    ctx.Writer.WriteEndElement();
                    break;
                case CodeInline code:
                    ctx.Writer.WriteStartElement("code");
                    ctx.Writer.WriteString(code.Content);
                    ctx.Writer.WriteEndElement();
                    break;
                case LinkInline link:
                    EmitLink(link, ctx);
                    break;
                case LineBreakInline:
                    ctx.Writer.WriteString("\n");
                    break;
                case ContainerInline ci:
                    EmitInlines(ci, ctx);
                    break;
            }
        }
    }

    private static void EmitLink(LinkInline link, XmlEmitterContext ctx)
    {
        if (link.IsImage)
        {
            ctx.Writer.WriteStartElement("image");
            ctx.Writer.WriteAttributeString("src", link.Url ?? "");
            ctx.Writer.WriteAttributeString("alt", GetPlainText(link));

            if (link.Title != null)
            {
                ctx.Writer.WriteAttributeString("title", link.Title);
            }

            ctx.Writer.WriteEndElement();
        }
        else
        {
            var href = link.Url ?? "";

            // Resolve internal .md links to .html
            if (ctx.LinkResolver != null && !string.IsNullOrEmpty(href))
            {
                var resolved = ctx.LinkResolver.Resolve(href, ctx.DocumentPath);
                href = resolved.ResolvedHref;
                if (resolved.IsBroken)
                {
                    ctx.Warnings.Add($"Broken link in {ctx.DocumentPath}: {link.Url}");
                }
            }

            ctx.Writer.WriteStartElement("link");
            ctx.Writer.WriteAttributeString("href", href);

            if (link.Title != null)
            {
                ctx.Writer.WriteAttributeString("title", link.Title);
            }

            EmitInlines(link, ctx);
            ctx.Writer.WriteEndElement();
        }
    }

    private static void EmitTable(Table table, XmlEmitterContext ctx,
        Dictionary<Type, ICrucibleExtension> dispatch)
    {
        ctx.Writer.WriteStartElement("table");
        var isFirstRow = true;
        var inHead = false;

        foreach (var row in table.OfType<TableRow>())
        {
            if (row.IsHeader && !inHead)
            {
                ctx.Writer.WriteStartElement("table-head");
                inHead = true;
            }
            else if (!row.IsHeader && inHead)
            {
                ctx.Writer.WriteEndElement(); // table-head
                ctx.Writer.WriteStartElement("table-body");
                inHead = false;
            }
            else if (!row.IsHeader && isFirstRow)
            {
                ctx.Writer.WriteStartElement("table-body");
            }

            ctx.Writer.WriteStartElement("row");

            foreach (var cell in row.OfType<TableCell>())
            {
                ctx.Writer.WriteStartElement("cell");

                if (row.IsHeader)
                {
                    ctx.Writer.WriteAttributeString("header", "true");
                }

                foreach (var child in cell)
                {
                    EmitBlock(child, ctx, dispatch);
                }

                ctx.Writer.WriteEndElement();
            }

            ctx.Writer.WriteEndElement(); // row
            isFirstRow = false;
        }

        if (inHead)
        {
            ctx.Writer.WriteEndElement(); // table-head (no body rows)
        }
        else
        {
            ctx.Writer.WriteEndElement(); // table-body
        }

        ctx.Writer.WriteEndElement(); // table
    }

    private static void EmitAdmonition(CustomContainer container,
        XmlEmitterContext ctx, Dictionary<Type, ICrucibleExtension> dispatch)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase — admonition types are lowercase by convention
        var type = container.Info?.Trim().ToLowerInvariant() ?? "note";
#pragma warning restore CA1308
        ctx.Writer.WriteStartElement("admonition");
        ctx.Writer.WriteAttributeString("type", type);

        foreach (var child in container)
        {
            EmitBlock(child, ctx, dispatch);
        }

        ctx.Writer.WriteEndElement();
    }

    private static string ExtractLines(LeafBlock block)
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

    private static string GetPlainText(ContainerInline? container)
    {
        if (container == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content);
                    break;
                case ContainerInline ci:
                    sb.Append(GetPlainText(ci));
                    break;
            }
        }

        return sb.ToString();
    }
}
