# `modern` Theme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a second built-in Crucible theme, `modern`, with a three-column layout, slate palette + blue accent, ⌘K search overlay, and dark code blocks with filename tab + copy button + bundled Prism highlighting.

**Architecture:** Three theme-agnostic emitter changes (code-block filename split, `toc` frontmatter, built-in theme name resolution) land first via TDD. Then a new theme directory at `src/Crucible.Core/Themes/modern/` ships with its own XSLT, CSS, and JS. An integration test proves the full pipeline works end-to-end; manual browser validation confirms the visual outcome.

**Tech Stack:** .NET 10, C#, xUnit v3, FluentAssertions, Markdig, PhoenixmlDb.Xslt (XSLT 3.0+), Prism.js (bundled), Lunr.js (CDN, matching `default`).

**Spec:** `docs/superpowers/specs/2026-04-15-modern-theme-design.md`

---

## File Structure

| File | Purpose | Create / Modify |
| ---- | ------- | --------------- |
| `src/Crucible.Core/Models/DocumentMetadata.cs` | Add `bool? Toc` property | Modify |
| `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs` | Split code fence info string; emit `@toc` | Modify |
| `src/Crucible.Core/Themes/ThemeLoader.cs` | Resolve built-in theme names | Modify |
| `src/Crucible.Core/Themes/modern/page.xslt` | 3-column layout | Create |
| `src/Crucible.Core/Themes/modern/sitemap.xslt` | Same as default | Create |
| `src/Crucible.Core/Themes/modern/css/style.css` | Palette, layout, admonitions, code | Create |
| `src/Crucible.Core/Themes/modern/css/prism.css` | Prism tomorrow-night theme | Create |
| `src/Crucible.Core/Themes/modern/js/theme.js` | Dark-mode toggle | Create |
| `src/Crucible.Core/Themes/modern/js/search.js` | ⌘K overlay (Lunr-backed) | Create |
| `src/Crucible.Core/Themes/modern/js/toc.js` | Right TOC scroll-spy | Create |
| `src/Crucible.Core/Themes/modern/js/copy.js` | Code-block copy button | Create |
| `src/Crucible.Core/Themes/modern/js/prism.js` | Prism core + 13 languages | Create (downloaded) |
| `tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs` | Tests for filename split & toc attr | Modify |
| `tests/Crucible.Core.Tests/Themes/ThemeLoaderTests.cs` | Built-in name resolution tests | Create |
| `tests/Crucible.Core.Tests/Pipeline/ModernThemeTests.cs` | End-to-end integration test | Create |
| `tests/Crucible.Core.Tests/Fixtures/modern-site/` | Small fixture doc tree | Create |

---

## Task 1: Emit `@filename` on fenced code blocks

**Files:**
- Modify: `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs` (method `EmitFencedCodeBlock`, lines 207-218)
- Test: `tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these tests to `tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs` (inside the existing `MarkdownToXmlEmitterTests` class, after the existing `Emit_FencedCodeBlock_ProducesCodeBlockElement` test):

```csharp
[Fact]
public void Emit_FencedCodeBlock_LanguageOnly_NoFilenameAttribute()
{
    var doc = Emit("```bash\necho hi\n```");
    var code = doc.Root!.Element("body")!.Element("code-block")!;
    code.Attribute("language")!.Value.Should().Be("bash");
    code.Attribute("filename").Should().BeNull();
}

[Fact]
public void Emit_FencedCodeBlock_WithTitleDoubleQuoted_ExtractsFilename()
{
    var doc = Emit("```bash title=\"install.sh\"\necho hi\n```");
    var code = doc.Root!.Element("body")!.Element("code-block")!;
    code.Attribute("language")!.Value.Should().Be("bash");
    code.Attribute("filename")!.Value.Should().Be("install.sh");
}

[Fact]
public void Emit_FencedCodeBlock_WithTitleSingleQuoted_ExtractsFilename()
{
    var doc = Emit("```bash title='install.sh'\necho hi\n```");
    var code = doc.Root!.Element("body")!.Element("code-block")!;
    code.Attribute("language")!.Value.Should().Be("bash");
    code.Attribute("filename")!.Value.Should().Be("install.sh");
}

[Fact]
public void Emit_FencedCodeBlock_WithUnknownTrailingArgs_IgnoresThem()
{
    var doc = Emit("```bash extra-junk=1\necho hi\n```");
    var code = doc.Root!.Element("body")!.Element("code-block")!;
    code.Attribute("language")!.Value.Should().Be("bash");
    code.Attribute("filename").Should().BeNull();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~Emit_FencedCodeBlock"`

Expected: The 4 new tests fail. The existing `Emit_FencedCodeBlock_ProducesCodeBlockElement` test may now also fail (`language` currently gets the raw info string including `title=...` for the new cases — but that test uses `csharp` alone, so it should still pass).

- [ ] **Step 3: Update `EmitFencedCodeBlock`**

Replace the body of `EmitFencedCodeBlock` in `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs` (currently lines 207-218) with:

```csharp
private static void EmitFencedCodeBlock(FencedCodeBlock fenced, XmlEmitterContext ctx)
{
    ctx.Writer.WriteStartElement("code-block");

    var info = fenced.Info;
    if (!string.IsNullOrEmpty(info))
    {
        var (language, filename) = ParseFenceInfo(info);
        if (!string.IsNullOrEmpty(language))
        {
            ctx.Writer.WriteAttributeString("language", language);
        }
        if (!string.IsNullOrEmpty(filename))
        {
            ctx.Writer.WriteAttributeString("filename", filename);
        }
    }

    ctx.Writer.WriteString(ExtractLines(fenced));
    ctx.Writer.WriteEndElement();
}

private static (string Language, string? Filename) ParseFenceInfo(string info)
{
    var trimmed = info.Trim();
    var firstSpace = trimmed.IndexOf(' ');
    if (firstSpace < 0)
    {
        return (trimmed, null);
    }

    var language = trimmed[..firstSpace];
    var rest = trimmed[(firstSpace + 1)..];

    // Match: title="..."  or  title='...'
    foreach (var quote in new[] { '"', '\'' })
    {
        var key = $"title={quote}";
        var start = rest.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) continue;
        var valueStart = start + key.Length;
        var valueEnd = rest.IndexOf(quote, valueStart);
        if (valueEnd < 0) continue;
        return (language, rest[valueStart..valueEnd]);
    }

    return (language, null);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~Emit_FencedCodeBlock"`

Expected: All 5 code-block tests pass.

- [ ] **Step 5: Run the full test suite to confirm no regressions**

Run: `dotnet test`

Expected: All tests pass (no existing test regressed).

- [ ] **Step 6: Commit**

```bash
git add src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs
git commit -m "feat(parser): extract filename from fenced code-block info string

Split the Markdig fenced-block info string into separate @language and
@filename attributes on <code-block>. Supports title=\"...\" and title='...'
forms; other trailing tokens are ignored. @filename is omitted when absent."
```

---

## Task 2: Frontmatter `toc: false` emits `<document toc="false">`

**Files:**
- Modify: `src/Crucible.Core/Models/DocumentMetadata.cs`
- Modify: `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs` (method `Emit`, line 50-58 area)
- Test: `tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `MarkdownToXmlEmitterTests.cs`:

```csharp
[Fact]
public void Emit_TocFalse_WritesTocAttribute()
{
    var metadata = new DocumentMetadata { Title = "T", Toc = false };
    var xml = MarkdownToXmlEmitter.Emit("# Hi", metadata, "test");
    var doc = XDocument.Parse(xml);
    doc.Root!.Attribute("toc")!.Value.Should().Be("false");
}

[Fact]
public void Emit_TocTrue_OmitsTocAttribute()
{
    var metadata = new DocumentMetadata { Title = "T", Toc = true };
    var xml = MarkdownToXmlEmitter.Emit("# Hi", metadata, "test");
    var doc = XDocument.Parse(xml);
    doc.Root!.Attribute("toc").Should().BeNull();
}

[Fact]
public void Emit_TocNull_OmitsTocAttribute()
{
    var metadata = new DocumentMetadata { Title = "T" };
    var xml = MarkdownToXmlEmitter.Emit("# Hi", metadata, "test");
    var doc = XDocument.Parse(xml);
    doc.Root!.Attribute("toc").Should().BeNull();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~Emit_Toc"`

Expected: All three fail — the first with a compile error (`Toc` property does not exist on `DocumentMetadata`).

- [ ] **Step 3: Add `Toc` property to `DocumentMetadata`**

Modify `src/Crucible.Core/Models/DocumentMetadata.cs`. Add the `Toc` property after `Template`:

```csharp
namespace Crucible.Core.Models;

#pragma warning disable CA1002 // Do not expose generic lists — DTO used for YAML deserialization
#pragma warning disable CA2227 // Collection properties should be read only — DTO used for YAML deserialization

public sealed class DocumentMetadata
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? Sort { get; set; }
    public DateTime? Updated { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool Draft { get; set; }
    public string? Template { get; set; }
    public bool? Toc { get; set; }

    [YamlDotNet.Serialization.YamlIgnore]
    public Dictionary<string, object?> Extra { get; set; } = [];
}
```

- [ ] **Step 4: Emit the `toc` attribute in `MarkdownToXmlEmitter.Emit`**

In `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs`, locate the `Emit` method's `<document>` attribute writes (currently lines 50-64). Immediately after the `description` attribute block (around line 58) and before the `Updated` block, add the `toc` attribute write:

```csharp
if (metadata.Description != null)
{
    writer.WriteAttributeString("description", metadata.Description);
}

if (metadata.Toc == false)
{
    writer.WriteAttributeString("toc", "false");
}

if (metadata.Updated.HasValue)
{
    writer.WriteAttributeString("updated",
        metadata.Updated.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~Emit_Toc"`

Expected: All 3 tests pass.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Crucible.Core/Models/DocumentMetadata.cs src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs
git commit -m "feat(parser): support 'toc: false' frontmatter flag

Add DocumentMetadata.Toc (bool?). When explicitly false, the emitter
writes toc=\"false\" on <document>. Themes can read this to suppress
the on-this-page sidebar for pages where it isn't useful."
```

---

## Task 3: `ThemeLoader` resolves built-in theme names

**Files:**
- Modify: `src/Crucible.Core/Themes/ThemeLoader.cs`
- Create: `tests/Crucible.Core.Tests/Themes/ThemeLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Crucible.Core.Tests/Themes/ThemeLoaderTests.cs`:

```csharp
namespace Crucible.Core.Tests.Themes;

using Crucible.Core.Themes;
using FluentAssertions;
using Xunit;

public class ThemeLoaderTests
{
    [Fact]
    public void Ctor_NullPath_ResolvesDefaultBuiltIn()
    {
        var loader = new ThemeLoader(null);
        loader.ThemeDirectory.Should()
            .EndWith(Path.Combine("Themes", "default"));
        loader.PageXslt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Ctor_BuiltInName_ResolvesAgainstAppContext()
    {
        var loader = new ThemeLoader("default");
        loader.ThemeDirectory.Should()
            .EndWith(Path.Combine("Themes", "default"));
        loader.PageXslt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Ctor_AbsoluteDirectory_UsesDirectAsIs()
    {
        var builtInDefault = Path.Combine(AppContext.BaseDirectory, "Themes", "default");
        var loader = new ThemeLoader(builtInDefault);
        loader.ThemeDirectory.Should().Be(builtInDefault);
    }

    [Fact]
    public void Ctor_UnknownBuiltInName_Throws()
    {
        var act = () => new ThemeLoader("does-not-exist");
        act.Should().Throw<DirectoryNotFoundException>();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~ThemeLoaderTests"`

Expected: `Ctor_BuiltInName_ResolvesAgainstAppContext` and `Ctor_UnknownBuiltInName_Throws` fail — today passing `"default"` is treated as a relative path and errors with a `FileNotFoundException` or similar (not a `DirectoryNotFoundException` from the resolver).

- [ ] **Step 3: Update `ThemeLoader`**

Replace the contents of `src/Crucible.Core/Themes/ThemeLoader.cs` with:

```csharp
namespace Crucible.Core.Themes;

public sealed class ThemeLoader
{
    public string PageXslt { get; }
    public string SitemapXslt { get; }
    public string ThemeDirectory { get; }

    public ThemeLoader(string? customThemePath = null)
    {
        ThemeDirectory = ResolveThemeDirectory(customThemePath);
        PageXslt = File.ReadAllText(Path.Combine(ThemeDirectory, "page.xslt"));
        SitemapXslt = File.ReadAllText(Path.Combine(ThemeDirectory, "sitemap.xslt"));
    }

    public IEnumerable<(string RelativePath, string FullPath)> GetStaticAssets()
    {
        var cssDir = Path.Combine(ThemeDirectory, "css");
        var jsDir = Path.Combine(ThemeDirectory, "js");
        foreach (var dir in new[] { cssDir, jsDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                yield return (Path.GetRelativePath(ThemeDirectory, file), file);
            }
        }
    }

    private static string ResolveThemeDirectory(string? customThemePath)
    {
        if (string.IsNullOrEmpty(customThemePath))
        {
            return GetBuiltInPath("default");
        }

        if (Directory.Exists(customThemePath))
        {
            return customThemePath;
        }

        var builtIn = GetBuiltInPath(customThemePath);
        if (Directory.Exists(builtIn))
        {
            return builtIn;
        }

        throw new DirectoryNotFoundException(
            $"Theme '{customThemePath}' not found as a directory or a built-in theme.");
    }

    private static string GetBuiltInPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Themes", name);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~ThemeLoaderTests"`

Expected: All 4 tests pass.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`

Expected: All tests pass (the change is backward-compatible — `null` still resolves `default`, and absolute directories still work).

- [ ] **Step 6: Commit**

```bash
git add src/Crucible.Core/Themes/ThemeLoader.cs tests/Crucible.Core.Tests/Themes/ThemeLoaderTests.cs
git commit -m "feat(themes): resolve built-in theme names in ThemeLoader

Passing a name like 'modern' or 'default' now resolves to
AppContext.BaseDirectory/Themes/<name>/. Existing behaviour
(null -> default, absolute path -> as-is) is preserved."
```

---

## Task 4: Scaffold the `modern/` theme directory

**Files:**
- Create: `src/Crucible.Core/Themes/modern/sitemap.xslt`
- Create: `src/Crucible.Core/Themes/modern/page.xslt` (placeholder)
- Create: `src/Crucible.Core/Themes/modern/css/style.css` (placeholder)
- Create: `src/Crucible.Core/Themes/modern/js/theme.js` (placeholder)

The `ThemeLoader` constructor reads both `page.xslt` and `sitemap.xslt` eagerly. This task creates minimal stubs so later tasks fill them in without blocking each other.

- [ ] **Step 1: Create `sitemap.xslt` (identical to default)**

Write to `src/Crucible.Core/Themes/modern/sitemap.xslt`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <xsl:template match="site">
    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <xsl:apply-templates select=".//page"/>
    </urlset>
  </xsl:template>

  <xsl:template match="page">
    <url xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <loc><xsl:value-of select="concat(ancestor::site/@base-url, @path, '.html')"/></loc>
      <xsl:if test="@updated">
        <lastmod><xsl:value-of select="@updated"/></lastmod>
      </xsl:if>
    </url>
  </xsl:template>
</xsl:stylesheet>
```

- [ ] **Step 2: Create a placeholder `page.xslt`**

Write to `src/Crucible.Core/Themes/modern/page.xslt` a minimal stub — replaced in full in Task 5 but needs to exist so the build artifact copies it:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="html" html-version="5" indent="yes" encoding="UTF-8"/>
  <xsl:template match="document">
    <html lang="en"><head><title><xsl:value-of select="@title"/></title></head>
      <body><xsl:apply-templates select="body/*"/></body>
    </html>
  </xsl:template>
</xsl:stylesheet>
```

- [ ] **Step 3: Create empty CSS and JS placeholders**

Write to `src/Crucible.Core/Themes/modern/css/style.css`:

```css
/* modern theme — filled in by Task 6 */
```

Write to `src/Crucible.Core/Themes/modern/js/theme.js`:

```js
// modern theme — filled in by Task 7
```

- [ ] **Step 4: Rebuild and verify assets are copied**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

Run: `ls src/Crucible.Core/bin/Debug/net*/Themes/`

Expected output: `default` and `modern` directories are present.

- [ ] **Step 5: Commit**

```bash
git add src/Crucible.Core/Themes/modern
git commit -m "chore(themes): scaffold modern theme directory

Adds sitemap.xslt (identical to default), a page.xslt stub,
and empty CSS/JS placeholders. Real content lands in the
following tasks."
```

---

## Task 5: `modern/page.xslt` — three-column layout

**Files:**
- Modify: `src/Crucible.Core/Themes/modern/page.xslt` (replace the stub)

- [ ] **Step 1: Replace `page.xslt` with the full layout**

Write to `src/Crucible.Core/Themes/modern/page.xslt`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="html" html-version="5" indent="yes" encoding="UTF-8"/>

  <xsl:param name="site-manifest-uri" select="''"/>
  <xsl:param name="base-url" select="'/'"/>
  <xsl:param name="site-title" select="'Documentation'"/>
  <xsl:param name="current-path" select="''"/>

  <xsl:variable name="manifest" select="if ($site-manifest-uri != '') then doc($site-manifest-uri) else ()"/>

  <xsl:template match="document">
    <html lang="en">
      <head>
        <meta charset="UTF-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
        <title><xsl:value-of select="@title"/> — <xsl:value-of select="$site-title"/></title>
        <xsl:if test="@description">
          <meta name="description" content="{@description}"/>
        </xsl:if>
        <link rel="canonical" href="{$base-url}{@path}.html"/>
        <meta property="og:title" content="{@title}"/>
        <meta property="og:type" content="article"/>
        <meta property="og:url" content="{$base-url}{@path}.html"/>
        <xsl:if test="@description">
          <meta property="og:description" content="{@description}"/>
        </xsl:if>
        <link rel="stylesheet" href="{$base-url}css/style.css"/>
        <link rel="stylesheet" href="{$base-url}css/prism.css"/>
        <script>
          (function(){var t=localStorage.getItem("crucible-theme");if(t)document.documentElement.setAttribute("data-theme",t)})();
        </script>
      </head>
      <body>
        <header class="site-header">
          <a href="{$base-url}index.html" class="site-logo"><xsl:value-of select="$site-title"/></a>
          <div class="header-actions">
            <button class="search-trigger" id="search-trigger" aria-label="Search documentation">
              <span class="search-icon">&#128269;</span>
              <span class="search-label">Search…</span>
              <kbd class="search-kbd">&#8984; K</kbd>
            </button>
            <button class="theme-toggle" id="theme-toggle" aria-label="Toggle dark mode" title="Toggle dark mode">&#9790;</button>
            <button class="nav-toggle" aria-label="Toggle navigation">&#9776;</button>
          </div>
        </header>

        <div class="layout">
          <nav class="sidebar" aria-label="Documentation">
            <xsl:if test="$manifest">
              <xsl:apply-templates select="$manifest/site" mode="nav"/>
            </xsl:if>
          </nav>

          <main>
            <article>
              <xsl:variable name="parent-section"
                select="if ($manifest) then ($manifest//page[@path = current()/@path]/parent::section/@title) else ()"/>
              <xsl:if test="$parent-section">
                <p class="eyebrow"><xsl:value-of select="$parent-section"/></p>
              </xsl:if>
              <h1 class="page-title"><xsl:value-of select="@title"/></h1>
              <xsl:if test="@description">
                <p class="page-subtitle"><xsl:value-of select="@description"/></p>
              </xsl:if>
              <xsl:apply-templates select="body/*"/>
            </article>
          </main>

          <xsl:if test="not(@toc = 'false')">
            <aside class="toc" aria-label="On this page">
              <p class="toc-label">On this page</p>
              <ul>
                <xsl:for-each select="body//heading[@level = ('2','3')]">
                  <li class="toc-h{@level}">
                    <a href="#{@id}"><xsl:value-of select="."/></a>
                  </li>
                </xsl:for-each>
              </ul>
            </aside>
          </xsl:if>
        </div>

        <div class="search-overlay" id="search-overlay" hidden="hidden">
          <div class="search-backdrop"></div>
          <div class="search-panel" role="dialog" aria-label="Search">
            <input type="search" id="search-input" placeholder="Search documentation…" aria-label="Search documentation" autocomplete="off"/>
            <div class="search-results" id="search-results"></div>
          </div>
        </div>

        <footer class="site-footer">
          <p>Built with <a href="https://github.com/phoenixmldb/crucible" target="_blank" noreferrer="noreferrer" noopener="noopener">Crucible</a> by <a href="https://endpointsystems.com" target="_blank" noreferrer="noreferrer" noopener="noopener">Endpoint Systems</a></p>
        </footer>

        <script src="https://unpkg.com/lunr/lunr.js"></script>
        <script src="{$base-url}js/prism.js"></script>
        <script src="{$base-url}js/search.js"></script>
        <script src="{$base-url}js/theme.js"></script>
        <script src="{$base-url}js/toc.js"></script>
        <script src="{$base-url}js/copy.js"></script>
      </body>
    </html>
  </xsl:template>

  <!-- Navigation templates -->
  <xsl:template match="site" mode="nav">
    <ul class="nav-tree">
      <xsl:apply-templates select="page|section" mode="nav"/>
    </ul>
  </xsl:template>

  <xsl:template match="section" mode="nav">
    <xsl:variable name="has-active" select="exists(.//page[@path = $current-path])"/>
    <li class="nav-section{if ($has-active) then ' open' else ''}">
      <p class="nav-section-title"><xsl:value-of select="@title"/></p>
      <ul class="nav-section-children">
        <xsl:apply-templates select="page|section" mode="nav"/>
      </ul>
    </li>
  </xsl:template>

  <xsl:template match="page" mode="nav">
    <li>
      <xsl:if test="@path = $current-path">
        <xsl:attribute name="class">active</xsl:attribute>
      </xsl:if>
      <a href="{$base-url}{@path}.html"><xsl:value-of select="@title"/></a>
    </li>
  </xsl:template>

  <!-- Body element templates -->
  <xsl:template match="heading">
    <xsl:element name="h{@level}">
      <xsl:attribute name="id"><xsl:value-of select="@id"/></xsl:attribute>
      <a class="anchor" href="#{@id}">#</a>
      <xsl:apply-templates/>
    </xsl:element>
  </xsl:template>

  <xsl:template match="paragraph">
    <p><xsl:apply-templates/></p>
  </xsl:template>

  <xsl:template match="code-block">
    <figure class="code">
      <xsl:if test="@filename or @language">
        <figcaption>
          <xsl:if test="@filename">
            <span class="filename"><xsl:value-of select="@filename"/></span>
          </xsl:if>
          <xsl:if test="@language">
            <span class="lang"><xsl:value-of select="@language"/></span>
          </xsl:if>
        </figcaption>
      </xsl:if>
      <pre><code>
        <xsl:if test="@language">
          <xsl:attribute name="class">language-<xsl:value-of select="@language"/></xsl:attribute>
        </xsl:if>
        <xsl:value-of select="."/>
      </code></pre>
      <button class="copy" type="button" aria-label="Copy code">Copy</button>
    </figure>
  </xsl:template>

  <xsl:template match="code">
    <code><xsl:value-of select="."/></code>
  </xsl:template>

  <xsl:template match="list[@type='unordered']">
    <ul><xsl:apply-templates select="item"/></ul>
  </xsl:template>

  <xsl:template match="list[@type='ordered']">
    <ol><xsl:apply-templates select="item"/></ol>
  </xsl:template>

  <xsl:template match="item">
    <li><xsl:apply-templates/></li>
  </xsl:template>

  <xsl:template match="link">
    <a href="{@href}">
      <xsl:if test="@title"><xsl:attribute name="title"><xsl:value-of select="@title"/></xsl:attribute></xsl:if>
      <xsl:apply-templates/>
    </a>
  </xsl:template>

  <xsl:template match="image">
    <img src="{@src}" alt="{@alt}">
      <xsl:if test="@title"><xsl:attribute name="title"><xsl:value-of select="@title"/></xsl:attribute></xsl:if>
    </img>
  </xsl:template>

  <xsl:template match="emphasis">
    <em><xsl:apply-templates/></em>
  </xsl:template>

  <xsl:template match="strong">
    <strong><xsl:apply-templates/></strong>
  </xsl:template>

  <xsl:template match="blockquote">
    <blockquote><xsl:apply-templates/></blockquote>
  </xsl:template>

  <xsl:template match="table">
    <div class="table-wrap"><table><xsl:apply-templates/></table></div>
  </xsl:template>

  <xsl:template match="table-head">
    <thead><xsl:apply-templates/></thead>
  </xsl:template>

  <xsl:template match="table-body">
    <tbody><xsl:apply-templates/></tbody>
  </xsl:template>

  <xsl:template match="row">
    <tr><xsl:apply-templates/></tr>
  </xsl:template>

  <xsl:template match="cell[@header='true']">
    <th><xsl:apply-templates/></th>
  </xsl:template>

  <xsl:template match="cell">
    <td><xsl:apply-templates/></td>
  </xsl:template>

  <xsl:template match="thematic-break">
    <hr/>
  </xsl:template>

  <xsl:template match="admonition">
    <div class="admonition admonition-{@type}">
      <p class="admonition-title">
        <xsl:value-of select="upper-case(substring(@type, 1, 1))"/>
        <xsl:value-of select="substring(@type, 2)"/>
      </p>
      <xsl:apply-templates/>
    </div>
  </xsl:template>

  <xsl:template match="mermaid">
    <div class="mermaid-wrapper">
      <pre class="mermaid"><xsl:value-of select="."/></pre>
    </div>
    <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
  </xsl:template>

</xsl:stylesheet>
```

- [ ] **Step 2: Rebuild**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Crucible.Core/Themes/modern/page.xslt
git commit -m "feat(themes/modern): three-column page layout

Emits header (logo / search trigger / theme toggle / nav toggle),
manifest-driven left sidebar, main article with eyebrow + title +
subtitle, right-side on-this-page TOC (skipped when document/@toc is
'false'), and the cmd+K search overlay skeleton. Code blocks render
as <figure class='code'> with figcaption + copy button, consumed by
Prism and copy.js on the client."
```

---

## Task 6: `modern/css/style.css` — full stylesheet

**Files:**
- Modify: `src/Crucible.Core/Themes/modern/css/style.css` (replace placeholder)

- [ ] **Step 1: Replace `style.css`**

Write to `src/Crucible.Core/Themes/modern/css/style.css`:

```css
/* ============================================================
   Crucible `modern` Theme
   Three-column docs layout inspired by cursor.com/docs.
   ============================================================ */

:root {
  --bg: #ffffff;
  --bg-subtle: #f8fafc;
  --fg: #0f172a;
  --fg-muted: #64748b;
  --border: #e2e8f0;
  --accent: #2563eb;
  --accent-hover: #1d4ed8;
  --accent-subtle: #eff6ff;
  --code-bg: #0b0d12;
  --code-fg: #d1d5db;
  --code-caption-bg: #111827;
  --code-caption-fg: #9ca3af;

  --header-height: 56px;
  --sidebar-width: 240px;
  --toc-width: 220px;
  --content-max: 720px;

  --font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Inter, Roboto, "Helvetica Neue", Arial, sans-serif;
  --font-mono: "SFMono-Regular", Consolas, "Liberation Mono", Menlo, Courier, monospace;
}

[data-theme="dark"] {
  --bg: #0b0f19;
  --bg-subtle: #111827;
  --fg: #e5e7eb;
  --fg-muted: #94a3b8;
  --border: #1e293b;
  --accent: #60a5fa;
  --accent-hover: #93c5fd;
  --accent-subtle: #1e3a8a33;
}

@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --bg: #0b0f19;
    --bg-subtle: #111827;
    --fg: #e5e7eb;
    --fg-muted: #94a3b8;
    --border: #1e293b;
    --accent: #60a5fa;
    --accent-hover: #93c5fd;
    --accent-subtle: #1e3a8a33;
  }
}

*, *::before, *::after { box-sizing: border-box; }
html { -webkit-text-size-adjust: 100%; }
body {
  margin: 0;
  font-family: var(--font-sans);
  font-size: 16px;
  line-height: 1.7;
  color: var(--fg);
  background: var(--bg);
}
a { color: var(--accent); text-decoration: none; }
a:hover { color: var(--accent-hover); }

article a { border-bottom: 1px solid transparent; transition: border-color .15s; }
article a:hover { border-bottom-color: currentColor; }

h1, h2, h3, h4 { line-height: 1.25; font-weight: 600; }
h1 { font-size: 30px; margin: 0 0 8px; }
h2 { font-size: 22px; margin: 40px 0 12px; }
h3 { font-size: 18px; margin: 28px 0 8px; }
h4 { font-size: 16px; margin: 20px 0 6px; }

p { margin: 0 0 16px; }

kbd {
  display: inline-block;
  padding: 2px 6px;
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--fg-muted);
  background: var(--bg-subtle);
  border: 1px solid var(--border);
  border-radius: 4px;
}

.site-header {
  position: sticky; top: 0; z-index: 20;
  height: var(--header-height);
  display: flex; align-items: center; justify-content: space-between;
  padding: 0 20px;
  background: var(--bg);
  border-bottom: 1px solid var(--border);
}
.site-logo { font-weight: 600; color: var(--fg); }
.header-actions { display: flex; align-items: center; gap: 10px; }
.search-trigger {
  display: flex; align-items: center; gap: 8px;
  padding: 6px 10px;
  background: var(--bg-subtle);
  border: 1px solid var(--border);
  border-radius: 6px;
  color: var(--fg-muted);
  cursor: pointer;
  min-width: 220px;
  font: inherit;
}
.search-trigger:hover { border-color: var(--accent); color: var(--fg); }
.search-trigger .search-label { flex: 1; text-align: left; }
.theme-toggle, .nav-toggle {
  background: transparent; border: 1px solid var(--border);
  color: var(--fg); border-radius: 6px;
  width: 34px; height: 34px; cursor: pointer;
}
.nav-toggle { display: none; }

.layout {
  display: grid;
  grid-template-columns: var(--sidebar-width) 1fr var(--toc-width);
  gap: 0;
  min-height: calc(100vh - var(--header-height));
}

.sidebar {
  border-right: 1px solid var(--border);
  padding: 24px 12px;
  font-size: 14px;
  overflow-y: auto;
  position: sticky; top: var(--header-height);
  height: calc(100vh - var(--header-height));
}
.nav-tree, .nav-section-children { list-style: none; padding: 0; margin: 0; }
.nav-section { margin: 14px 0; }
.nav-section-title {
  margin: 0 0 6px;
  padding: 0 8px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 11px;
  color: var(--fg-muted);
}
.nav-tree li a {
  display: block;
  padding: 4px 8px;
  color: var(--fg-muted);
  border-left: 2px solid transparent;
  border-radius: 0 4px 4px 0;
}
.nav-tree li a:hover { color: var(--fg); background: var(--bg-subtle); }
.nav-tree li.active a {
  color: var(--accent);
  background: var(--accent-subtle);
  border-left-color: var(--accent);
  font-weight: 500;
}

main { min-width: 0; padding: 32px 40px; }
article { max-width: var(--content-max); margin: 0 auto; }
.eyebrow {
  text-transform: uppercase; letter-spacing: 0.1em;
  font-size: 11px; color: var(--fg-muted); margin: 0 0 4px;
}
.page-title { margin-top: 0; }
.page-subtitle { color: var(--fg-muted); font-size: 17px; margin-bottom: 24px; }
.anchor {
  opacity: 0; margin-right: 6px; color: var(--fg-muted);
  font-weight: normal; transition: opacity .15s;
}
h1:hover .anchor, h2:hover .anchor, h3:hover .anchor { opacity: 1; }

.toc {
  border-left: 1px solid var(--border);
  padding: 32px 20px;
  font-size: 13px;
  position: sticky; top: var(--header-height);
  height: calc(100vh - var(--header-height));
  overflow-y: auto;
}
.toc-label {
  text-transform: uppercase; letter-spacing: 0.08em;
  font-size: 11px; color: var(--fg-muted); margin: 0 0 10px;
}
.toc ul { list-style: none; padding: 0; margin: 0; }
.toc li { padding: 3px 0; }
.toc li.toc-h3 { padding-left: 12px; }
.toc a { color: var(--fg-muted); display: block; border-left: 2px solid transparent; padding-left: 8px; margin-left: -10px; }
.toc a:hover { color: var(--fg); }
.toc a.active { color: var(--accent); border-left-color: var(--accent); }

code {
  font-family: var(--font-mono);
  font-size: 14px;
  padding: 1px 5px;
  background: var(--bg-subtle);
  border: 1px solid var(--border);
  border-radius: 4px;
}
figure.code {
  position: relative;
  margin: 20px 0;
  background: var(--code-bg);
  border-radius: 8px;
  overflow: hidden;
}
figure.code figcaption {
  display: flex; justify-content: space-between; align-items: center;
  padding: 8px 14px;
  background: var(--code-caption-bg);
  color: var(--code-caption-fg);
  font-family: var(--font-mono);
  font-size: 12px;
  border-bottom: 1px solid rgba(255,255,255,0.06);
}
figure.code figcaption .filename { color: #e5e7eb; }
figure.code figcaption .lang { text-transform: lowercase; }
figure.code pre {
  margin: 0;
  padding: 14px 16px;
  overflow-x: auto;
  color: var(--code-fg);
  background: var(--code-bg);
  font-size: 14px;
  line-height: 1.6;
}
figure.code pre code {
  background: transparent;
  border: 0;
  padding: 0;
  color: inherit;
  font-size: inherit;
}
figure.code button.copy {
  position: absolute; top: 8px; right: 10px;
  padding: 3px 9px;
  background: rgba(255,255,255,0.08);
  color: #e5e7eb;
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 4px;
  font-size: 11px;
  cursor: pointer;
  opacity: 0;
  transition: opacity .15s;
}
figure.code:hover button.copy, figure.code button.copy:focus { opacity: 1; }
figure.code button.copy.copied { background: rgba(37,99,235,0.35); }

.admonition {
  border-left: 3px solid var(--fg-muted);
  background: var(--bg-subtle);
  padding: 12px 16px;
  margin: 20px 0;
  border-radius: 0 6px 6px 0;
}
.admonition-title { margin: 0 0 6px; font-weight: 600; font-size: 14px; }
.admonition-note    { border-left-color: #2563eb; }
.admonition-tip     { border-left-color: #059669; }
.admonition-warning { border-left-color: #d97706; }
.admonition-danger  { border-left-color: #dc2626; }

.table-wrap { overflow-x: auto; margin: 20px 0; }
table { border-collapse: collapse; width: 100%; font-size: 14px; }
th, td { padding: 8px 12px; border-bottom: 1px solid var(--border); text-align: left; }
th { font-weight: 600; background: var(--bg-subtle); }

hr { border: 0; border-top: 1px solid var(--border); margin: 32px 0; }
blockquote {
  border-left: 3px solid var(--border);
  margin: 16px 0; padding: 4px 14px;
  color: var(--fg-muted);
}

.search-overlay {
  position: fixed; inset: 0; z-index: 100;
  display: flex; align-items: flex-start; justify-content: center;
  padding-top: 14vh;
}
.search-overlay[hidden] { display: none; }
.search-backdrop {
  position: absolute; inset: 0;
  background: rgba(15, 23, 42, 0.55);
  backdrop-filter: blur(2px);
}
.search-panel {
  position: relative;
  width: min(600px, 90vw);
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: 10px;
  box-shadow: 0 20px 50px -12px rgba(0,0,0,0.35);
  overflow: hidden;
}
#search-input {
  width: 100%;
  padding: 14px 16px;
  border: 0;
  border-bottom: 1px solid var(--border);
  background: var(--bg);
  color: var(--fg);
  font-size: 15px;
  outline: none;
  font-family: inherit;
}
.search-results { max-height: 60vh; overflow-y: auto; }
.search-result {
  display: block;
  padding: 10px 16px;
  border-bottom: 1px solid var(--border);
  color: var(--fg);
  cursor: pointer;
}
.search-result:last-child { border-bottom: 0; }
.search-result.active, .search-result:hover { background: var(--accent-subtle); }
.search-result-title { font-weight: 600; font-size: 14px; }
.search-result-path { font-size: 12px; color: var(--fg-muted); }
.search-result-snippet { font-size: 13px; color: var(--fg-muted); margin-top: 4px; }
.search-empty, .search-none {
  padding: 20px 16px;
  color: var(--fg-muted);
  font-size: 14px;
  text-align: center;
}

.site-footer {
  border-top: 1px solid var(--border);
  padding: 16px 20px;
  text-align: center;
  color: var(--fg-muted);
  font-size: 13px;
}
.site-footer a { color: var(--fg-muted); border-bottom: 1px dotted var(--border); }
.site-footer a:hover { color: var(--accent); }

@media (max-width: 1280px) {
  .layout { grid-template-columns: var(--sidebar-width) 1fr; }
  .toc { display: none; }
}
@media (max-width: 768px) {
  .layout { grid-template-columns: 1fr; }
  .sidebar {
    position: fixed; top: var(--header-height); left: 0;
    width: 80vw; max-width: 320px;
    height: calc(100vh - var(--header-height));
    background: var(--bg);
    transform: translateX(-100%);
    transition: transform .2s;
    z-index: 15;
    border-right: 1px solid var(--border);
  }
  body.nav-open .sidebar { transform: translateX(0); }
  .nav-toggle { display: inline-flex; align-items: center; justify-content: center; }
  .search-trigger { min-width: 0; }
  .search-trigger .search-label, .search-trigger .search-kbd { display: none; }
  main { padding: 20px 16px; }
}
```

- [ ] **Step 2: Build to verify the stylesheet ships**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

Run: `ls src/Crucible.Core/bin/Debug/net*/Themes/modern/css/`

Expected output: `style.css` listed.

- [ ] **Step 3: Commit**

```bash
git add src/Crucible.Core/Themes/modern/css/style.css
git commit -m "feat(themes/modern): full stylesheet

Slate + blue palette with dark mode, 3-column grid (240/main/220),
dark code blocks with filename caption and hover-reveal copy button,
admonition styles for note/tip/warning/danger, search overlay chrome,
and mobile breakpoints that collapse the right TOC at 1280px and the
left sidebar into a drawer at 768px."
```

---

## Task 7: `modern/js/theme.js` — dark-mode toggle

**Files:**
- Modify: `src/Crucible.Core/Themes/modern/js/theme.js` (replace placeholder)

- [ ] **Step 1: Replace `theme.js`**

Write to `src/Crucible.Core/Themes/modern/js/theme.js`:

```js
(function () {
  var KEY = "crucible-theme";
  var root = document.documentElement;
  var toggle = document.getElementById("theme-toggle");
  var navToggle = document.querySelector(".nav-toggle");

  function current() {
    return root.getAttribute("data-theme") ||
      (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  }

  if (toggle) {
    toggle.addEventListener("click", function () {
      var next = current() === "dark" ? "light" : "dark";
      root.setAttribute("data-theme", next);
      try { localStorage.setItem(KEY, next); } catch (e) {}
    });
  }

  if (navToggle) {
    navToggle.addEventListener("click", function () {
      document.body.classList.toggle("nav-open");
    });
  }
})();
```

- [ ] **Step 2: Rebuild**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Crucible.Core/Themes/modern/js/theme.js
git commit -m "feat(themes/modern): dark-mode and nav-drawer toggles"
```

---

## Task 8: `modern/js/toc.js` — right-side TOC scroll-spy

**Files:**
- Create: `src/Crucible.Core/Themes/modern/js/toc.js`

- [ ] **Step 1: Write `toc.js`**

Write to `src/Crucible.Core/Themes/modern/js/toc.js`:

```js
(function () {
  var toc = document.querySelector(".toc");
  if (!toc) return;

  var links = toc.querySelectorAll("a[href^='#']");
  if (!links.length) return;

  var byId = {};
  links.forEach(function (a) {
    var id = a.getAttribute("href").slice(1);
    byId[id] = a;
  });

  var headings = document.querySelectorAll("article h2[id], article h3[id]");
  if (!headings.length) return;

  var observer = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (!e.isIntersecting) return;
      var a = byId[e.target.id];
      if (!a) return;
      links.forEach(function (l) { l.classList.remove("active"); });
      a.classList.add("active");
    });
  }, { rootMargin: "-10% 0px -70% 0px", threshold: 0 });

  headings.forEach(function (h) { observer.observe(h); });
})();
```

- [ ] **Step 2: Rebuild**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Crucible.Core/Themes/modern/js/toc.js
git commit -m "feat(themes/modern): scroll-spy for right-side TOC"
```

---

## Task 9: `modern/js/copy.js` — code-block copy button

**Files:**
- Create: `src/Crucible.Core/Themes/modern/js/copy.js`

- [ ] **Step 1: Write `copy.js`**

Write to `src/Crucible.Core/Themes/modern/js/copy.js`:

```js
(function () {
  document.querySelectorAll("figure.code button.copy").forEach(function (btn) {
    btn.addEventListener("click", function () {
      var code = btn.parentElement.querySelector("pre code");
      if (!code || !navigator.clipboard) return;
      navigator.clipboard.writeText(code.textContent || "").then(function () {
        var original = btn.textContent;
        btn.textContent = "Copied";
        btn.classList.add("copied");
        setTimeout(function () {
          btn.textContent = original;
          btn.classList.remove("copied");
        }, 1500);
      }).catch(function () {});
    });
  });
})();
```

- [ ] **Step 2: Rebuild**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Crucible.Core/Themes/modern/js/copy.js
git commit -m "feat(themes/modern): copy-to-clipboard button for code blocks"
```

---

## Task 10: `modern/js/search.js` — ⌘K search overlay

**Files:**
- Create: `src/Crucible.Core/Themes/modern/js/search.js`

All result rendering uses `document.createElement` + `textContent` — no `innerHTML` — to avoid XSS on arbitrary index content.

- [ ] **Step 1: Write `search.js`**

Write to `src/Crucible.Core/Themes/modern/js/search.js`:

```js
(function () {
  var overlay = document.getElementById("search-overlay");
  var trigger = document.getElementById("search-trigger");
  var input = document.getElementById("search-input");
  var results = document.getElementById("search-results");
  var backdrop = overlay && overlay.querySelector(".search-backdrop");
  if (!overlay || !trigger || !input || !results) return;

  var idx = null;
  var docs = null;
  var docsByPath = {};
  var loading = false;
  var active = -1;

  function ensureIndex() {
    if (idx || loading) return Promise.resolve();
    loading = true;
    return fetch("search-index.json").then(function (r) {
      if (!r.ok) throw new Error("search-index.json missing");
      return r.json();
    }).then(function (data) {
      docs = data.documents || [];
      docs.forEach(function (d) { docsByPath[d.path] = d; });
      idx = lunr(function () {
        this.ref("path");
        this.field("title", { boost: 5 });
        this.field("content");
        var self = this;
        docs.forEach(function (d) { self.add(d); });
      });
    }).finally(function () { loading = false; });
  }

  function open() {
    overlay.hidden = false;
    ensureIndex().then(function () {
      input.value = "";
      render("");
      input.focus();
    });
  }

  function close() {
    overlay.hidden = true;
    active = -1;
  }

  function snippet(text, query) {
    if (!text) return "";
    var q = query.trim().split(/\s+/)[0];
    if (!q) return text.slice(0, 140);
    var at = text.toLowerCase().indexOf(q.toLowerCase());
    if (at < 0) return text.slice(0, 140);
    var start = Math.max(0, at - 30);
    return (start > 0 ? "… " : "") + text.slice(start, start + 140);
  }

  function clearChildren(node) {
    while (node.firstChild) node.removeChild(node.firstChild);
  }

  function makeMessage(cls, text) {
    var div = document.createElement("div");
    div.className = cls;
    div.textContent = text;
    return div;
  }

  function makeResult(doc, query, isActive) {
    var a = document.createElement("a");
    a.className = "search-result" + (isActive ? " active" : "");
    a.href = doc.path + ".html";

    var title = document.createElement("div");
    title.className = "search-result-title";
    title.textContent = doc.title || doc.path;

    var path = document.createElement("div");
    path.className = "search-result-path";
    path.textContent = doc.path;

    var snip = document.createElement("div");
    snip.className = "search-result-snippet";
    snip.textContent = snippet(doc.content, query);

    a.appendChild(title);
    a.appendChild(path);
    a.appendChild(snip);
    return a;
  }

  function render(query) {
    clearChildren(results);
    active = -1;

    if (!query) {
      results.appendChild(makeMessage("search-empty", "Start typing to search…"));
      return;
    }

    var matches = idx ? idx.search(query + "*") : [];
    if (!matches.length) {
      results.appendChild(makeMessage("search-none", "No results"));
      return;
    }

    matches.slice(0, 20).forEach(function (m, i) {
      var doc = docsByPath[m.ref];
      if (!doc) return;
      results.appendChild(makeResult(doc, query, i === 0));
    });
    active = 0;
  }

  function move(delta) {
    var items = results.querySelectorAll(".search-result");
    if (!items.length) return;
    if (active >= 0) items[active].classList.remove("active");
    active = (active + delta + items.length) % items.length;
    items[active].classList.add("active");
    items[active].scrollIntoView({ block: "nearest" });
  }

  function activate() {
    var items = results.querySelectorAll(".search-result");
    if (active >= 0 && items[active]) items[active].click();
  }

  trigger.addEventListener("click", open);
  if (backdrop) backdrop.addEventListener("click", close);

  input.addEventListener("input", function () { render(input.value); });
  input.addEventListener("keydown", function (e) {
    if (e.key === "ArrowDown") { e.preventDefault(); move(1); }
    else if (e.key === "ArrowUp") { e.preventDefault(); move(-1); }
    else if (e.key === "Enter") { e.preventDefault(); activate(); }
    else if (e.key === "Escape") { e.preventDefault(); close(); }
  });

  document.addEventListener("keydown", function (e) {
    var isK = (e.key === "k" || e.key === "K") && (e.metaKey || e.ctrlKey);
    var active = document.activeElement;
    var isSlash = e.key === "/" && !e.metaKey && !e.ctrlKey &&
                  active && active.tagName !== "INPUT" && active.tagName !== "TEXTAREA";
    if (isK || isSlash) { e.preventDefault(); open(); }
    else if (e.key === "Escape" && !overlay.hidden) { close(); }
  });
})();
```

- [ ] **Step 2: Rebuild**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Crucible.Core/Themes/modern/js/search.js
git commit -m "feat(themes/modern): cmd+K search overlay

Lunr-backed overlay opened with cmd+K / ctrl+K / '/'. Results show
title, path, and a content snippet. Keyboard: up/down to move, Enter
to navigate, Esc to close. Result rendering uses createElement +
textContent (no innerHTML) so arbitrary index content can't inject
markup. Index is built lazily on first open from the existing
search-index.json."
```

---

## Task 11: `modern/js/prism.js` + `modern/css/prism.css` — bundled Prism

**Files:**
- Create: `src/Crucible.Core/Themes/modern/js/prism.js`
- Modify: `src/Crucible.Core/Themes/modern/css/prism.css` (create from CDN)

Prism's sibling NPM CDN provides ready-made copies of each language component. We assemble the bundle from the CDN so the download is reproducible.

**Language set** (from spec): `markup`, `css`, `clike` (required dep), `javascript`, `typescript`, `bash`, `shell-session`, `csharp`, `yaml`, `json`, `diff`, `markdown`, `python`, `xml-doc`.

- [ ] **Step 1: Download the Prism core + components**

Run from the repo root:

```bash
PRISM_VER=1.29.0
TMP=$(mktemp -d)
BASE=https://cdn.jsdelivr.net/npm/prismjs@${PRISM_VER}

curl -fsSL ${BASE}/prism.min.js                              -o ${TMP}/00-core.js
curl -fsSL ${BASE}/components/prism-markup.min.js            -o ${TMP}/10-markup.js
curl -fsSL ${BASE}/components/prism-css.min.js               -o ${TMP}/11-css.js
curl -fsSL ${BASE}/components/prism-clike.min.js             -o ${TMP}/12-clike.js
curl -fsSL ${BASE}/components/prism-javascript.min.js        -o ${TMP}/13-javascript.js
curl -fsSL ${BASE}/components/prism-typescript.min.js        -o ${TMP}/14-typescript.js
curl -fsSL ${BASE}/components/prism-bash.min.js              -o ${TMP}/15-bash.js
curl -fsSL ${BASE}/components/prism-shell-session.min.js     -o ${TMP}/16-shell-session.js
curl -fsSL ${BASE}/components/prism-csharp.min.js            -o ${TMP}/17-csharp.js
curl -fsSL ${BASE}/components/prism-yaml.min.js              -o ${TMP}/18-yaml.js
curl -fsSL ${BASE}/components/prism-json.min.js              -o ${TMP}/19-json.js
curl -fsSL ${BASE}/components/prism-diff.min.js              -o ${TMP}/20-diff.js
curl -fsSL ${BASE}/components/prism-markdown.min.js          -o ${TMP}/21-markdown.js
curl -fsSL ${BASE}/components/prism-python.min.js            -o ${TMP}/22-python.js
curl -fsSL ${BASE}/components/prism-xml-doc.min.js           -o ${TMP}/23-xml-doc.js

# Concatenate in dependency order (names sort lexically).
cat ${TMP}/*.js > src/Crucible.Core/Themes/modern/js/prism.js

curl -fsSL ${BASE}/themes/prism-tomorrow.min.css             -o src/Crucible.Core/Themes/modern/css/prism.css
```

Expected: both files exist and are non-empty. Verify with:

```bash
wc -c src/Crucible.Core/Themes/modern/js/prism.js src/Crucible.Core/Themes/modern/css/prism.css
```

Expected: `prism.js` in the 80–150 KB range; `prism.css` 1–3 KB.

- [ ] **Step 2: Sanity-check the bundle**

Run: `head -c 120 src/Crucible.Core/Themes/modern/js/prism.js`

Expected: starts with minified Prism IIFE (begins with `var _self="undefined"!=typeof window` or similar).

Run: `head -c 120 src/Crucible.Core/Themes/modern/css/prism.css`

Expected: starts with a `code[class*="language-"]` rule.

- [ ] **Step 3: Rebuild**

Run: `dotnet build src/Crucible.Core/Crucible.Core.csproj`

Expected: build succeeds. Verify both ship:

Run: `ls src/Crucible.Core/bin/Debug/net*/Themes/modern/css/ src/Crucible.Core/bin/Debug/net*/Themes/modern/js/`

Expected: `prism.css`, `prism.js`, `style.css`, `theme.js`, `toc.js`, `copy.js`, `search.js` all present.

- [ ] **Step 4: Commit**

```bash
git add src/Crucible.Core/Themes/modern/js/prism.js src/Crucible.Core/Themes/modern/css/prism.css
git commit -m "feat(themes/modern): bundled Prism.js (v1.29.0) + tomorrow theme

Concatenates Prism core + components for markup, css, clike,
javascript, typescript, bash, shell-session, csharp, yaml, json,
diff, markdown, python, xml-doc. Paired with the prism-tomorrow
theme stylesheet. Third-party code under MIT; see prismjs.com."
```

---

## Task 12: Integration test — `ModernThemeTests.cs`

**Files:**
- Create: `tests/Crucible.Core.Tests/Fixtures/modern-site/index.md`
- Create: `tests/Crucible.Core.Tests/Fixtures/modern-site/guides/install.md`
- Create: `tests/Crucible.Core.Tests/Fixtures/modern-site/guides/no-toc.md`
- Create: `tests/Crucible.Core.Tests/Pipeline/ModernThemeTests.cs`

- [ ] **Step 1: Create fixture — `index.md`**

Write to `tests/Crucible.Core.Tests/Fixtures/modern-site/index.md`:

```markdown
---
title: Welcome
description: Start here.
sort: 1
---

# Welcome

Top-level landing page for the modern-theme fixture.

## Overview

Some prose.

## Features

More prose.
```

- [ ] **Step 2: Create fixture — `guides/install.md`**

Write to `tests/Crucible.Core.Tests/Fixtures/modern-site/guides/install.md`:

````markdown
---
title: Install
description: Install the tool.
sort: 1
---

# Install

Install the CLI.

## Prerequisites

Requires .NET 10.

## Install the CLI

```bash title="install.sh"
dotnet tool install -g Crucible.Cli
```

## Verify

Check the version.
````

- [ ] **Step 3: Create fixture — `guides/no-toc.md`**

Write to `tests/Crucible.Core.Tests/Fixtures/modern-site/guides/no-toc.md`:

```markdown
---
title: Landing
description: Landing-style page with TOC disabled.
toc: false
sort: 2
---

# Landing

## Section A

Text.

## Section B

Text.
```

- [ ] **Step 4: Write the integration test**

Create `tests/Crucible.Core.Tests/Pipeline/ModernThemeTests.cs`:

```csharp
namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;
using Xunit;

public class ModernThemeTests
{
    private static async Task<string> BuildAndReadAsync(string relativeHtmlPath)
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "modern-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ct = TestContext.Current.CancellationToken;

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Modern Site", baseUrl: "/",
                extensions: [], includeDrafts: false, ct: ct);
            parseResult.Success.Should().BeTrue();

            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir, themePath: "modern", extensions: [], ct: ct);
            transformResult.Success.Should().BeTrue();

            return await File.ReadAllTextAsync(Path.Combine(outputDir, relativeHtmlPath), ct);
        }
        finally
        {
            if (Directory.Exists(intermediateDir)) Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Build_DefaultPage_IncludesRightSideToc()
    {
        var html = await BuildAndReadAsync("index.html");
        html.Should().Contain("class=\"toc\"");
        html.Should().Contain("On this page");
        html.Should().Contain("#overview");
        html.Should().Contain("#features");
    }

    [Fact]
    public async Task Build_PageWithTocFalse_OmitsRightSideToc()
    {
        var html = await BuildAndReadAsync(Path.Combine("guides", "no-toc.html"));
        html.Should().NotContain("class=\"toc\"");
    }

    [Fact]
    public async Task Build_CodeBlockWithTitle_RendersFigcaption()
    {
        var html = await BuildAndReadAsync(Path.Combine("guides", "install.html"));
        html.Should().Contain("<figure class=\"code\"");
        html.Should().Contain("<span class=\"filename\">install.sh</span>");
        html.Should().Contain("language-bash");
    }

    [Fact]
    public async Task Build_ModernTheme_RendersSearchTrigger()
    {
        var html = await BuildAndReadAsync("index.html");
        html.Should().Contain("id=\"search-trigger\"");
        html.Should().Contain("id=\"search-overlay\"");
    }

    [Fact]
    public async Task Build_ModernTheme_LinksToThemeAssets()
    {
        var html = await BuildAndReadAsync("index.html");
        html.Should().Contain("css/style.css");
        html.Should().Contain("css/prism.css");
        html.Should().Contain("js/prism.js");
        html.Should().Contain("js/search.js");
        html.Should().Contain("js/toc.js");
        html.Should().Contain("js/copy.js");
    }
}
```

- [ ] **Step 5: Run the new integration tests**

Run: `dotnet test tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj --filter "FullyQualifiedName~ModernThemeTests"`

Expected: All 5 tests pass.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`

Expected: All tests pass (`default`-theme tests must not regress).

- [ ] **Step 7: Commit**

```bash
git add tests/Crucible.Core.Tests/Fixtures/modern-site tests/Crucible.Core.Tests/Pipeline/ModernThemeTests.cs
git commit -m "test(themes/modern): end-to-end pipeline integration

Three-page fixture exercising: a default page (TOC present),
a code-block with a title= attribute (figcaption filename),
and a page with 'toc: false' (no TOC aside). Also asserts
the cmd+K search overlay and all theme asset references are
present in the rendered HTML."
```

---

## Task 13: Manual visual validation

**Files:** none modified; this is a smoke-test step.

- [ ] **Step 1: Build a sample site with the new theme**

Run:

```bash
rm -rf /tmp/modern-demo
mkdir -p /tmp/modern-demo
cp -r tests/Crucible.Core.Tests/Fixtures/modern-site/* /tmp/modern-demo/
dotnet run --project src/Crucible.Cli -- build --source /tmp/modern-demo --output /tmp/modern-demo/dist --theme modern
```

Expected: build succeeds; `/tmp/modern-demo/dist/` contains `index.html`, `guides/install.html`, `guides/no-toc.html`, `sitemap.xml`, `search-index.json`, plus the `css/` and `js/` assets.

- [ ] **Step 2: Serve locally and open**

Run:

```bash
cd /tmp/modern-demo/dist && python3 -m http.server 8765 &
SERVER_PID=$!
```

Open http://localhost:8765 in a browser and verify:

- Header shows logo + search trigger (with `⌘K` hint) + theme toggle
- Left sidebar shows `guides/` section with `Install` and `Landing` pages
- Right-side TOC shows `Overview` and `Features` on the home page
- Press ⌘K (macOS) or Ctrl+K → overlay opens, input focuses
- Type `install` → `Install` page appears as a result; Enter navigates to it
- On `Install`, the `install.sh` code block shows a filename caption, a `bash` language tag, dark background, syntax highlighting, and a "Copy" button on hover that flashes "Copied"
- On `Install`, scrolling updates the right TOC highlight (scroll-spy)
- Open `Landing` page → right TOC is **not** rendered
- Toggle the moon icon → light/dark palettes swap; reload preserves the choice
- Narrow the window below ~1280px → right TOC disappears
- Narrow below ~768px → left sidebar becomes a drawer; hamburger opens it

- [ ] **Step 3: Stop the server**

Run:

```bash
kill $SERVER_PID
```

- [ ] **Step 4: Build the same site with the default theme as a regression check**

Run:

```bash
rm -rf /tmp/modern-demo/dist
dotnet run --project src/Crucible.Cli -- build --source /tmp/modern-demo --output /tmp/modern-demo/dist
```

Expected: build succeeds; `/tmp/modern-demo/dist/index.html` opens and renders in the familiar `default` styling (no regression from the emitter changes).

- [ ] **Step 5: If everything above passed, this task is done**

No commit — there is nothing to save. If any manual check failed, open an issue or fix in a follow-up commit on this branch.

---

## Self-Review Notes

- **Spec coverage:** §Scope → Task 4-11. §File layout → Task 4. §Layout → Task 5. §Visual style → Task 6. §Code blocks (emitter split) → Task 1; (rendering) → Task 5/6; (Prism) → Task 11; (copy button) → Task 9. §Search → Task 10. §Right-side TOC (XSLT) → Task 5; (opt-out emitter) → Task 2; (scroll-spy) → Task 8. §Changes to existing code → Tasks 1, 2, 3. §Testing → Tasks 1, 2, 3, 12, 13.
- **Placeholder scan:** Clean — no "TBD" / "similar to" / "write tests for the above" patterns; each step contains concrete code or commands.
- **Type consistency:** `DocumentMetadata.Toc` used identically in Task 2 steps 3 and 4. `ThemeLoader(string?)` signature preserved in Task 3. `<code-block>` → `<figure class="code">` render shape matches between spec, Task 5 XSLT, Task 6 CSS, Task 9 copy.js, and Task 12 assertions. Search element IDs (`search-trigger`, `search-overlay`, `search-input`, `search-results`) align across Task 5 (markup), Task 6 (CSS), Task 10 (JS), and Task 12 (assertions). TOC classes (`toc`, `toc-label`, `toc-h2`, `toc-h3`) align across Task 5, 6, 8, 12.
- **Security:** Task 10 search.js renders all result content via `createElement` + `textContent`; no `innerHTML` on user-controlled data.
