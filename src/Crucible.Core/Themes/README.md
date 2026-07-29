# Built-in themes

```
Themes/
  _base/          shared XSLT — not a theme, never copied to site output
    elements.xslt   body-element templates + the mermaid-scripts template
    sitemap.xslt    sitemap.xml generator
  default/        classic two-column layout
  modern/         three-column layout with ⌘K search, TOC scroll-spy, Prism
```

Each theme directory must contain `page.xslt` and `sitemap.xslt`. Everything
under its `css/` and `js/` is copied verbatim into the generated site;
`ThemeLoader` walks only those two directories, which is why `_base` never ships.

## Sharing vs overriding

`default/page.xslt` and `modern/page.xslt` both start with:

```xml
<xsl:import href="../_base/elements.xslt"/>
```

`xsl:import` gives imported templates *lower* precedence, so a theme overrides
any shared template by declaring the same match pattern in its own `page.xslt`.
The modern theme does this for `code-block` (adds a filename caption and copy
button) and `table` (adds a horizontal-scroll wrapper).

`xsl:import` must be the first declaration in the stylesheet, before
`xsl:output` and `xsl:param`.

**Custom themes** (`theme: ./my-theme` in `crucible.yaml`) live outside this
directory and cannot import `_base`. They must be self-contained.

## Selecting a theme

Built-in themes are resolved by bare name (`theme: modern`). A name containing a
path separator or `..`, or starting with `_`, is never resolved as a built-in —
point at those with an explicit directory path instead.

## Vendored assets

Third-party JavaScript is vendored rather than loaded from a CDN, so that
generated sites work offline, behind a strict CSP, and do not break when an
unpinned CDN URL moves. Regenerate with:

```bash
# lunr 2.3.9 — search index/query engine, used by both themes
curl -o default/js/lunr.js https://cdn.jsdelivr.net/npm/lunr@2.3.9/lunr.min.js
cp default/js/lunr.js modern/js/lunr.js

# Prism 1.30.0 — syntax highlighting, modern theme only.
# Core carries markup/xml/html/svg, css, clike, javascript; the components
# below add the languages that show up in .NET and docs-site content.
V=1.30.0
curl -o /tmp/core.js "https://cdn.jsdelivr.net/npm/prismjs@$V/prism.min.js"
for c in bash csharp yaml json sql typescript python powershell docker diff markdown; do
  curl -o "/tmp/c-$c.js" "https://cdn.jsdelivr.net/npm/prismjs@$V/components/prism-$c.min.js"
done
{
  cat /tmp/core.js; echo
  for c in bash csharp yaml json sql typescript python powershell docker diff markdown; do
    cat "/tmp/c-$c.js"; echo
  done
  echo 'Prism.languages.xslt = Prism.languages.xsl = Prism.languages.markup;'
} > modern/js/prism.js
```

Keep the header comment at the top of `modern/js/prism.js` when regenerating.
Component order matters: `csharp` needs `clike` and `typescript` needs
`javascript`, both of which live in core.

The mermaid runtime is the one script still loaded from a CDN — it is several
megabytes and only a minority of pages need it. It is pinned to an exact version
with an SRI hash in `_base/elements.xslt`; when bumping the version, recompute:

```bash
curl -sL https://cdn.jsdelivr.net/npm/mermaid@<version>/dist/mermaid.min.js \
  | openssl dgst -sha384 -binary | openssl base64 -A
```
