namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;
using Xunit;

public class LinkResolverTests
{
    [Fact]
    public void Resolve_RelativeMdLink_RewritesToHtml()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation", "getting-started/quick-start"]);

        var result = resolver.Resolve("../getting-started/installation.md",
            currentPath: "xslt/overview");

        result.ResolvedHref.Should().Be("../getting-started/installation.html");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_RootRelativeMdLink_RewritesToHtml()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation"]);

        var result = resolver.Resolve("/getting-started/installation.md",
            currentPath: "xslt/deep/nested/page");

        result.ResolvedHref.Should().Be("/getting-started/installation.html");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_BrokenLink_MarksAsBroken()
    {
        var resolver = new LinkResolver(["index"]);

        var result = resolver.Resolve("nonexistent.md", currentPath: "index");

        result.IsBroken.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ExternalLink_PassesThrough()
    {
        var resolver = new LinkResolver([]);

        var result = resolver.Resolve("https://example.com", currentPath: "index");

        result.ResolvedHref.Should().Be("https://example.com");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_AnchorLink_PreservesFragment()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation"]);

        var result = resolver.Resolve("installation.md#requirements",
            currentPath: "getting-started/overview");

        result.ResolvedHref.Should().Be("installation.html#requirements");
        result.IsBroken.Should().BeFalse();
    }
}
