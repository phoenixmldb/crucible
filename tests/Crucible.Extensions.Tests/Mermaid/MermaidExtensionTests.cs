namespace Crucible.Extensions.Tests.Mermaid;

using System.Text;
using System.Xml;
using Crucible.Core.Extensions;
using Crucible.Extensions.Mermaid;
using FluentAssertions;
using Markdig;
using Xunit;
using Markdig.Syntax;

public class MermaidExtensionTests
{
    [Fact]
    public void CanProcess_FencedCodeBlock_ReturnsTrue()
    {
        new MermaidExtension().CanProcess(typeof(FencedCodeBlock)).Should().BeTrue();
    }

    [Fact]
    public void CanProcess_OtherNodeType_ReturnsFalse()
    {
        new MermaidExtension().CanProcess(typeof(ParagraphBlock)).Should().BeFalse();
    }

    [Fact]
    public void ProcessNode_MermaidBlock_EmitsMermaidElement()
    {
        var md = "```mermaid\ngraph LR; A-->B;\n```";
        var doc = Markdown.Parse(md);
        var block = doc.Descendants<FencedCodeBlock>().First();

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true });
        var context = new XmlEmitterContext { Writer = writer, DocumentPath = "test" };

        var handled = new MermaidExtension().ProcessNode(block, context);
        writer.Flush();

        handled.Should().BeTrue();
        sb.ToString().Should().Contain("<mermaid>");
        sb.ToString().Should().Contain("graph LR");
    }

    [Fact]
    public void ProcessNode_NonMermaidCodeBlock_ReturnsFalse()
    {
        var md = "```csharp\nvar x = 1;\n```";
        var doc = Markdown.Parse(md);
        var block = doc.Descendants<FencedCodeBlock>().First();

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb);
        var context = new XmlEmitterContext { Writer = writer, DocumentPath = "test" };

        new MermaidExtension().ProcessNode(block, context).Should().BeFalse();
    }

    [Fact]
    public void GetAssets_ReturnsMermaidJsInitializer()
    {
        var assets = new MermaidExtension().GetAssets().ToList();
        assets.Should().ContainSingle();
        assets[0].RelativePath.Should().Contain("mermaid");
        assets[0].ContentType.Should().Be("application/javascript");
    }
}
