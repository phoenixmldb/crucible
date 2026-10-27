#pragma warning disable CA1054 // URI parameters should not be strings — base-url is a
                               // path prefix ("/docs/") as often as an absolute URL, and
                               // the XSLT half concatenates strings. Same call as the
                               // CA1056 suppression on CrucibleConfig.BaseUrl.

namespace Crucible.Core.Urls;

using System.Text.RegularExpressions;

/// <summary>
/// The C# half of the one rule that turns a page path into a URL.
///
/// <para>
/// themes/_base/urls.xslt holds the other half and calls itself "the single
/// definition", which was true of everything written in XSLT — nav links,
/// canonical/og:url, sitemap &lt;loc&gt; — and of search.js, which mirrors it in
/// JavaScript. It was never true of the two producers written in C#:
/// <see cref="Parsing.LinkResolver"/> rewrote in-body <c>.md</c> cross-links to
/// <c>.html</c>, and <see cref="Search.LlmsTxtGenerator"/> appended <c>.html</c>
/// to every entry in llms.txt and llms-full.txt.
/// </para>
///
/// <para>
/// A host that serves clean URLs — Cloudflare Pages, Netlify, S3 with a rewrite
/// rule — answers those with a 308 to the extensionless form. Nothing breaks and
/// nothing is lost: the redirect resolves and the canonical page indexes. What it
/// costs is a redirect hop on every internal link, crawl budget spent re-learning
/// the same answer, and a Search Console report where the real findings sit
/// underneath several dozen phantom duplicates of pages that are already fine.
/// </para>
///
/// <para>
/// Both halves live on because a C# library cannot call an XPath function and the
/// XSLT cannot call this. PageUrlTests pins them together by text, the same way
/// LinkFormatTests already pins search.js.
/// </para>
/// </summary>
public static partial class PageUrl
{
    /// <summary>
    /// Collapses a trailing <c>index</c> segment to the directory it indexes, so
    /// a section landing page and its directory share one URL:
    /// <c>index</c> → <c>""</c>, <c>guides/index</c> → <c>guides/</c>,
    /// <c>guides/install</c> → unchanged.
    /// </summary>
    /// <param name="path">
    /// A page path, already extensionless — page paths carry no extension
    /// anywhere in the pipeline, so there is none to strip here.
    /// </param>
    public static string CollapseIndex(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return TrailingIndexSegment().Replace(path, "$1");
    }

    /// <summary>
    /// The URL for a page path under a base URL: the exact mirror of
    /// <c>c:page-url</c>, down to being a plain concatenation.
    /// </summary>
    public static string FromPath(string baseUrl, string path)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        return string.Concat(baseUrl, CollapseIndex(path));
    }

    // Anchored at both ends on purpose. Without (^|/) this would eat the tail of
    // "reindex"; without $ it would eat a leading "index/" directory. And it is
    // case-sensitive because XPath replace() is: the two halves have to agree
    // about a file named Index.md as much as about anything else.
    [GeneratedRegex("(^|/)index$")]
    private static partial Regex TrailingIndexSegment();
}
