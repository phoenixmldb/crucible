namespace Crucible.Core.Tests.Parsing;

using System.Xml.Linq;
using Crucible.Core.Models;
using Crucible.Core.Parsing;
using FluentAssertions;
using Xunit;

public class MarkdownToXmlEmitterTests
{
    private static XDocument Emit(string markdown, string path = "test",
        DocumentMetadata? metadata = null, LinkResolver? linkResolver = null)
    {
        metadata ??= new DocumentMetadata { Title = "Test" };
        var xml = MarkdownToXmlEmitter.Emit(markdown, metadata, path,
            linkResolver: linkResolver);
        return XDocument.Parse(xml);
    }

    [Fact]
    public void Emit_Heading_ProducesHeadingElement()
    {
        var doc = Emit("# Hello World");
        var heading = doc.Root!.Element("body")!.Element("heading")!;
        heading.Attribute("level")!.Value.Should().Be("1");
        heading.Attribute("id")!.Value.Should().Be("hello-world");
        heading.Value.Should().Be("Hello World");
    }

    [Fact]
    public void Emit_Paragraph_ProducesParagraphElement()
    {
        var doc = Emit("Some text here.");
        doc.Root!.Element("body")!.Element("paragraph")!.Value
            .Should().Be("Some text here.");
    }

    [Fact]
    public void Emit_FencedCodeBlock_ProducesCodeBlockElement()
    {
        var doc = Emit("```csharp\nvar x = 1;\n```");
        var code = doc.Root!.Element("body")!.Element("code-block")!;
        code.Attribute("language")!.Value.Should().Be("csharp");
        code.Value.Should().Contain("var x = 1;");
    }

    [Fact]
    public void Emit_UnorderedList_ProducesListElement()
    {
        var doc = Emit("- Item A\n- Item B");
        var list = doc.Root!.Element("body")!.Element("list")!;
        list.Attribute("type")!.Value.Should().Be("unordered");
        list.Elements("item").Should().HaveCount(2);
    }

    [Fact]
    public void Emit_InlineCode_ProducesCodeElement()
    {
        var doc = Emit("Use `dotnet build` to compile.");
        var para = doc.Root!.Element("body")!.Element("paragraph")!;
        para.Element("code")!.Value.Should().Be("dotnet build");
    }

    [Fact]
    public void Emit_Link_ProducesLinkElement()
    {
        var doc = Emit("[Click here](https://example.com)");
        var link = doc.Root!.Element("body")!.Element("paragraph")!.Element("link")!;
        link.Attribute("href")!.Value.Should().Be("https://example.com");
        link.Value.Should().Be("Click here");
    }

    [Fact]
    public void Emit_DocumentAttributes_IncludesMetadata()
    {
        var metadata = new DocumentMetadata
        {
            Title = "My Page",
            Description = "A test page",
            Updated = new DateTime(2026, 3, 15),
            Tags = ["test", "demo"]
        };
        var doc = Emit("Content", metadata: metadata);
        doc.Root!.Attribute("title")!.Value.Should().Be("My Page");
        doc.Root!.Attribute("description")!.Value.Should().Be("A test page");
        doc.Root!.Element("meta")!.Elements("tag").Should().HaveCount(2);
    }

    [Fact]
    public void Emit_Table_ProducesTableElements()
    {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var doc = Emit(md);
        var table = doc.Root!.Element("body")!.Element("table")!;
        table.Element("table-head").Should().NotBeNull();
        table.Element("table-body").Should().NotBeNull();
    }

    [Fact]
    public void Emit_Emphasis_ProducesEmphasisElement()
    {
        var doc = Emit("This is *italic* text.");
        doc.Root!.Element("body")!.Element("paragraph")!
            .Element("emphasis")!.Value.Should().Be("italic");
    }

    [Fact]
    public void Emit_Strong_ProducesStrongElement()
    {
        var doc = Emit("This is **bold** text.");
        doc.Root!.Element("body")!.Element("paragraph")!
            .Element("strong")!.Value.Should().Be("bold");
    }

    [Fact]
    public void Emit_Image_ProducesImageElement()
    {
        var doc = Emit("![Alt text](image.png \"Title\")");
        var img = doc.Root!.Element("body")!.Element("paragraph")!.Element("image")!;
        img.Attribute("src")!.Value.Should().Be("image.png");
        img.Attribute("alt")!.Value.Should().Be("Alt text");
        img.Attribute("title")!.Value.Should().Be("Title");
    }

    [Fact]
    public void Emit_Admonition_ProducesAdmonitionElement()
    {
        var doc = Emit("::: note\nThis is important.\n:::");
        var admonition = doc.Root!.Element("body")!.Element("admonition")!;
        admonition.Attribute("type")!.Value.Should().Be("note");
    }

    [Fact]
    public void Emit_Blockquote_ProducesBlockquoteElement()
    {
        var doc = Emit("> This is a quote.");
        doc.Root!.Element("body")!.Element("blockquote").Should().NotBeNull();
    }

    [Fact]
    public void Emit_ThematicBreak_ProducesThematicBreakElement()
    {
        var doc = Emit("Above\n\n***\n\nBelow");
        doc.Root!.Element("body")!.Descendants("thematic-break").Should().HaveCount(1);
    }

    [Fact]
    public void Emit_InternalMdLink_RewrittenToHtml()
    {
        var resolver = new LinkResolver(["getting-started/installation"]);
        var doc = Emit("[Install](getting-started/installation.md)",
            path: "index", linkResolver: resolver);
        var link = doc.Root!.Element("body")!.Element("paragraph")!.Element("link")!;
        link.Attribute("href")!.Value.Should().Be("getting-started/installation.html");
    }

    [Fact]
    public void Emit_BrokenLink_CollectsWarning()
    {
        var resolver = new LinkResolver(["index"]);
        var warnings = new List<string>();
        var metadata = new DocumentMetadata { Title = "Test" };
        MarkdownToXmlEmitter.Emit("[Missing](nonexistent.md)", metadata, "index",
            linkResolver: resolver, warnings: warnings);
        warnings.Should().ContainSingle().Which.Should().Contain("Broken link");
    }

    [Fact]
    public void Emit_ExternalLink_NotRewritten()
    {
        var resolver = new LinkResolver(["index"]);
        var doc = Emit("[Google](https://google.com)", path: "index",
            linkResolver: resolver);
        var link = doc.Root!.Element("body")!.Element("paragraph")!.Element("link")!;
        link.Attribute("href")!.Value.Should().Be("https://google.com");
    }
}
