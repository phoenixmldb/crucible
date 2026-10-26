namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;
using Xunit;

/// <summary>
/// In-body .md cross-links resolve to the same URL shape as nav, canonical and
/// sitemap: extensionless, with a trailing "index" segment collapsed to its
/// directory. See PageUrlTests for why the two halves of that rule are pinned
/// to each other.
/// </summary>
public class LinkResolverTests
{
    [Fact]
    public void Resolve_RelativeMdLink_RewritesToExtensionlessUrl()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation", "getting-started/quick-start"]);

        var result = resolver.Resolve("../getting-started/installation.md",
            currentPath: "xslt/overview");

        result.ResolvedHref.Should().Be("../getting-started/installation");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_RootRelativeMdLink_RewritesToExtensionlessUrl()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation"]);

        var result = resolver.Resolve("/getting-started/installation.md",
            currentPath: "xslt/deep/nested/page");

        result.ResolvedHref.Should().Be("/getting-started/installation");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_DirectoryIndexLink_CollapsesToTheDirectory()
    {
        var resolver = new LinkResolver(["getting-started/index"]);

        var result = resolver.Resolve("getting-started/index.md", currentPath: "index");

        // Not "getting-started/index": that URL 308s to the directory on every
        // static host that serves clean URLs, which is the redirect being removed.
        result.ResolvedHref.Should().Be("getting-started/");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_SiblingIndexLink_BecomesDotSlash()
    {
        var resolver = new LinkResolver(["getting-started/index", "getting-started/overview"]);

        var result = resolver.Resolve("index.md", currentPath: "getting-started/overview");

        // Collapsing the only segment leaves nothing, and an empty href means
        // "this page" — the one case where the bare rule needs a floor.
        result.ResolvedHref.Should().Be("./");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ParentIndexLink_CollapsesToParentDirectory()
    {
        var resolver = new LinkResolver(["index", "getting-started/overview"]);

        var result = resolver.Resolve("../index.md", currentPath: "getting-started/overview");

        result.ResolvedHref.Should().Be("../");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_RootIndexLink_CollapsesToRoot()
    {
        var resolver = new LinkResolver(["index"]);

        var result = resolver.Resolve("/index.md", currentPath: "getting-started/overview");

        result.ResolvedHref.Should().Be("/");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_IndexLinkWithFragment_KeepsBothTheSlashAndTheFragment()
    {
        var resolver = new LinkResolver(["getting-started/index", "getting-started/overview"]);

        var result = resolver.Resolve("index.md#requirements",
            currentPath: "getting-started/overview");

        result.ResolvedHref.Should().Be("./#requirements");
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
    public void Resolve_NonMarkdownLink_PassesThroughUntouched()
    {
        var resolver = new LinkResolver(["index"]);

        // Asset paths are not pages and must keep their extension. A rule written
        // as "strip the extension" rather than "rewrite .md" would break every
        // image, download and stylesheet link in the docs.
        var result = resolver.Resolve("../images/diagram.png", currentPath: "guides/overview");

        result.ResolvedHref.Should().Be("../images/diagram.png");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_AnchorLink_PreservesFragment()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation"]);

        var result = resolver.Resolve("installation.md#requirements",
            currentPath: "getting-started/overview");

        result.ResolvedHref.Should().Be("installation#requirements");
        result.IsBroken.Should().BeFalse();
    }
}
