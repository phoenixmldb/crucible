# Crucible — Static Documentation Generator

**Date:** 2026-03-19
**Status:** Approved
**Repo:** `crucible` (new public repo under phoenixmldb org)

## Overview

Crucible is a reusable static documentation generator that transforms Markdown source files into a static HTML site through an XML intermediate representation and XSLT 4.0 transformation. It demonstrates the PhoenixmlDb XSLT engine in a real-world product while serving as a general-purpose documentation tool.

### Goals

- Author-friendly: Markdown with YAML frontmatter, no XML knowledge required
- Showcase PhoenixmlDb XSLT 4.0 capabilities in production use
- Reusable by others with different documentation needs
- SEO-first static HTML output
- Extensible via a plugin model
- Hosting-agnostic (Cloudflare Pages primary target)

### Non-Goals (v1)

- PhoenixmlDb database integration at runtime
- Dynamic/server-rendered documentation
- Full theme marketplace
- WYSIWYG authoring

## Pipeline Architecture

```
Source Dir (.md) → Parse (MD → XML) → Transform (XSLT → HTML) → Static Site
```

Three stages, each independently observable via the `--stage` CLI flag.

### Stage 1: Parse

1. Discover `.md` files recursively from source directory
2. For each file: split YAML frontmatter from Markdown content
3. Parse Markdown via Markdig into AST
4. Run registered extensions against the AST (e.g., Mermaid annotates fenced code blocks)
5. Walk AST → emit XML document conforming to Crucible's document schema
6. Build site manifest XML: navigation tree inferred from directory structure, ordered by `sort` field in frontmatter
7. Output: directory of `.xml` files + `site-manifest.xml`

### Intermediate Directory Structure

Stage 1 writes to the output directory with this layout:

```
intermediate/
├── site-manifest.xml
├── index.xml
├── getting-started/
│   ├── installation.xml
│   └── quick-start.xml
└── xslt/
    └── overview.xml
```

The directory mirrors the source structure with `.md` replaced by `.xml`. When `--stage transform` is invoked, `--source` points to this intermediate directory. The pipeline auto-detects the input type: if `site-manifest.xml` exists in the source directory, it treats the input as XML intermediate; otherwise it treats it as Markdown source.

### Stage 2: Transform

1. Load theme XSLT stylesheet(s)
2. For each document XML: transform via `XsltTransformer` → HTML. The site manifest is passed to the XSLT as a stylesheet parameter (`site-manifest`) loaded via `doc()`, so `page.xslt` has access to both the current document (primary input) and the full site structure (parameter).
3. Site manifest drives navigation generation (sidebar, breadcrumbs, previous/next links)
4. Generate ancillary files: `sitemap.xml` (via `sitemap.xslt` transforming the manifest), Open Graph meta tags per page
5. Copy static assets (theme CSS/JS, extension assets, user assets)
6. Output: complete static site directory

### Stage 3: Full Build

Runs Stage 1 then Stage 2 in sequence, writing final output to the destination directory.

## Project Structure

```
crucible/
├── Directory.Build.props
├── Directory.Build.rsp
├── Directory.Packages.props
├── Crucible.slnx
├── README.md
├── LICENSE
├── src/
│   ├── Crucible.Core/             # Pipeline engine, interfaces, schema, themes
│   ├── Crucible.Extensions/       # Built-in extensions, plugin discovery
│   └── Crucible.Cli/              # dotnet tool entry point
└── tests/
    ├── Crucible.Core.Tests/
    └── Crucible.Extensions.Tests/
```

### Dependencies

| Project | Key Dependencies |
|---------|-----------------|
| Crucible.Core | Markdig, YamlDotNet, PhoenixmlDb.Xslt |
| Crucible.Extensions | Crucible.Core |
| Crucible.Cli | Crucible.Core, Crucible.Extensions |

### Packaging

- **Crucible.Core** → NuGet library (for programmatic use and third-party extension development)
- **Crucible.Extensions** → NuGet library (built-in extensions)
- **Crucible.Cli** → `PackAsTool=true`, `ToolCommandName=crucible`

### Conventions

Follows established PhoenixmlDb repo patterns:
- .NET 10 (`net10.0`), C# 14 (`LangVersion=preview`)
- `TreatWarningsAsErrors=true`
- Centralized package management
- Embedded debug symbols, deterministic builds
- Endpoint Systems metadata

## XML Schema

The intermediate XML is intentionally minimal — enough structure for XSLT, not so rigid that it constrains.

### Design Principles

- Element names are readable English (`paragraph` not `p`, `heading` not `h`)
- Markdown constructs map 1:1 to XML elements with no information loss
- Extensions can introduce new elements — the schema is open, not closed
- Custom frontmatter fields pass through as `<meta>` children
- No XML namespaces on the intermediate document XML — keeps XSLT authoring simple (XSLT stylesheets themselves naturally use the `xsl:` namespace)
- No DTD or XSD enforcement at runtime — validated by convention
- No presentation hints — that's XSLT's job

### Site Manifest (`site-manifest.xml`)

```xml
<site title="PhoenixmlDb Documentation" base-url="https://docs.phoenixmldb.com">
  <page path="index" title="Home" sort="0" />
  <section path="getting-started" title="Getting Started" sort="1">
    <page path="getting-started/installation" title="Installation" sort="1" />
    <page path="getting-started/quick-start" title="Quick Start" sort="2" />
  </section>
  <section path="xslt" title="XSLT 4.0" sort="2">
    <page path="xslt/overview" title="Overview" sort="1" />
    <section path="xslt/instructions" title="Instructions" sort="2">
      <page path="xslt/instructions/for-each" title="xsl:for-each" sort="1" />
    </section>
  </section>
</site>
```

- Sections map to directories, pages map to `.md` files
- Nesting is unbounded, mirrors the filesystem
- `sort` comes from YAML frontmatter; when omitted, items sort alphabetically by filename. Pages with explicit `sort` values are ordered first (ascending), followed by unsorted pages alphabetically

### Document XML (per page)

```xml
<document path="getting-started/installation"
          title="Installation"
          description="How to install PhoenixmlDb"
          updated="2026-03-15">
  <meta>
    <tag>getting-started</tag>
    <tag>setup</tag>
  </meta>
  <body>
    <heading level="1" id="installation">Installation</heading>
    <paragraph>Install via NuGet:</paragraph>
    <code-block language="bash">dotnet add package PhoenixmlDb</code-block>
    <heading level="2" id="requirements">Requirements</heading>
    <paragraph>You need <code>.NET 10</code> or later.</paragraph>
    <list type="unordered">
      <item><paragraph>.NET 10 SDK</paragraph></item>
      <item><paragraph>A supported OS</paragraph></item>
    </list>
    <admonition type="note">
      <paragraph>ICU globalization must be enabled.</paragraph>
    </admonition>
    <mermaid>graph LR; A--&gt;B;</mermaid>
  </body>
</document>
```

### Standard Body Elements

| Element | Attributes | Description |
|---------|-----------|-------------|
| `heading` | `level` (1-6), `id` | Section heading with auto-generated anchor slug |
| `paragraph` | | Block of text, may contain inline elements |
| `code-block` | `language` | Fenced code block |
| `code` | | Inline code |
| `list` | `type` (unordered, ordered) | List container |
| `item` | | List item |
| `link` | `href`, `title` | Hyperlink |
| `image` | `src`, `alt`, `title` | Image |
| `emphasis` | | Italic text |
| `strong` | | Bold text |
| `blockquote` | | Block quotation |
| `table` | | Table container |
| `table-head` | | Table header row group |
| `table-body` | | Table body row group |
| `row` | | Table row |
| `cell` | `align` (left, center, right), `header` (bool) | Table cell |
| `thematic-break` | | Horizontal rule |
| `admonition` | `type` (note, warning, tip, important, caution) | Callout block |

Extensions may add additional elements (e.g., `<mermaid>`).

### Standard Frontmatter Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | yes | Page title |
| `description` | string | no | Page description (used for SEO meta tag) |
| `sort` | integer | no | Sort order within parent section (default: alphabetical) |
| `updated` | date | no | Last updated date |
| `tags` | string[] | no | Tags for categorization |
| `draft` | bool | no | If true, excluded from build output |
| `template` | string | no | Override XSLT template: filename in the theme directory (e.g., `api-reference.xslt`) that replaces `page.xslt` for this page |

Additional fields are preserved as `<meta>` children in the XML.

## Extension Model

### Interface (Crucible.Core)

```csharp
public interface ICrucibleExtension
{
    string Name { get; }

    // Return true if this extension handles the given node type.
    // Called once at startup to build a dispatch table.
    bool CanProcess(Type markdigNodeType);

    // Called during AST walk for nodes this extension claimed.
    // The extension writes XML to the context's XmlWriter.
    // Return true if the walker should skip descending into children
    // (extension handled them), false to let the walker continue.
    bool ProcessNode(MarkdownObject node, XmlEmitterContext context);

    // Contribute static assets (JS, CSS) to the output.
    IEnumerable<CrucibleAsset> GetAssets();
}

// Provides XML writing capabilities to extensions during AST walk.
public sealed class XmlEmitterContext
{
    public XmlWriter Writer { get; }       // Write XML elements directly
    public string DocumentPath { get; }     // Current document's relative path
    public SiteManifest Manifest { get; }   // Site structure for cross-references
}

public record CrucibleAsset(string RelativePath, string ContentType, byte[] Content);
```

### How It Works

1. At startup, each registered extension is queried via `CanProcess()` to build a node-type → extension dispatch table. If two extensions claim the same node type, the last registered extension wins (built-in extensions register first, so user extensions can override).
2. During Stage 1, the AST walker encounters each Markdig node
3. Before emitting default XML, it checks the dispatch table for a matching extension
4. If matched, the extension's `ProcessNode()` is called. The extension writes XML via `context.Writer` and returns `true` to handle children itself, or `false` to let the walker descend normally.
5. Extensions can also contribute static assets to the build output via `GetAssets()`

### Mermaid Extension (Crucible.Extensions)

- Claims `FencedCodeBlock` nodes where `Info == "mermaid"`
- Emits `<mermaid>` XML element with the raw diagram text
- Contributes a JS asset that initializes mermaid.js on `<div class="mermaid">` elements in the browser
- Client-side rendering — demonstrates JS ecosystem compatibility

### Plugin Discovery

- Built-in extensions in `Crucible.Extensions` are registered by default
- Third-party extensions: assemblies placed in a `plugins/` directory relative to the project root (next to `crucible.yaml`), or specified by assembly name in the config `extensions:` list
- Loading uses a dedicated `AssemblyLoadContext` per plugin to isolate dependencies. Assemblies are scanned for public types implementing `ICrucibleExtension` and instantiated via parameterless constructor
- If a plugin fails to load, the build emits a warning and continues (unless `--strict` is used, in which case it fails)

### Third-Party Extension Development

Third parties reference `Crucible.Core`, implement `ICrucibleExtension`, ship as a NuGet package. Clean dependency story — no need to reference `Crucible.Extensions` or `Crucible.Cli`.

## Theme System

### Theme Structure

```
themes/
└── default/
    ├── page.xslt           # Main page template (document XML → HTML)
    ├── navigation.xslt     # Nav from site manifest → sidebar/breadcrumbs
    ├── sitemap.xslt         # sitemap.xml from site manifest
    ├── css/
    │   └── style.css
    └── js/
        └── theme.js         # Minimal JS (mobile nav toggle, search)
```

### XSLT Responsibilities

**`page.xslt`** — Main transform, produces complete HTML5 documents:
- Semantic HTML5 (`<article>`, `<nav>`, `<header>`, `<main>`, `<footer>`)
- Open Graph `<meta>` tags from document metadata
- Heading anchors for deep linking
- Code block syntax highlighting CSS classes
- Imports `navigation.xslt` for sidebar/breadcrumb rendering
- Receives document XML and site manifest as inputs

**`navigation.xslt`** — Sidebar and breadcrumbs from site manifest:
- Current page highlighting
- Collapsible section nesting
- Previous/next page links

**`sitemap.xslt`** — Standard sitemap protocol XML from site manifest:
- `lastmod` from document `updated` field

### HTML Output Qualities

- Semantic markup throughout — no `<div>` soup
- CSS custom properties for easy color/font customization
- Responsive by default
- `<link rel="canonical">`, proper `<title>`, `<meta name="description">` on every page
- Minimal JS, progressive enhancement only

### Theme Override

- Users specify a custom theme directory via `--theme` flag or config
- Custom themes can `xsl:import` the default and override specific templates, or replace entirely

## CLI Interface

### Installation

```bash
dotnet tool install -g Crucible.Cli
```

### Commands

```
crucible init [options]           # Scaffold crucible.yaml and starter docs
crucible build [options]          # Full pipeline or staged build
crucible serve [options]          # Local dev server (v1: optional/future)
```

### `crucible init`

Generates a starter `crucible.yaml` with commented defaults. If the source directory doesn't exist, scaffolds `docs/index.md` with example frontmatter.

| Flag | Short | Description |
|------|-------|-------------|
| `--force` | `-f` | Overwrite existing `crucible.yaml` |

### `crucible build`

| Flag | Short | Description | Default |
|------|-------|-------------|---------|
| `--source` | `-s` | Source directory | `./docs` |
| `--output` | `-o` | Output directory | `./dist` |
| `--stage` | | Stop at: `parse`, `transform` | full build |
| `--theme` | `-t` | Custom theme directory | built-in default |
| `--base-url` | | Base URL for sitemap/canonical | `/` |
| `--title` | | Site title (precedence: flag > config > root index.md) | from root `index.md` |
| `--verbose` | `-v` | Verbose output | `false` |
| `--timing` | | Stage timing breakdown | `false` |
| `--clean` | | Delete output dir before build | `false` |
| `--version` | | Show version | |
| `--include-drafts` | | Include pages marked `draft: true` | `false` |
| `--strict` | | Fail on warnings (broken links, plugin errors) | `false` |
| `--help` | `-h` | Show help | |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Usage error (bad args, missing source) |
| 2 | Parse error (invalid Markdown/YAML) |
| 3 | Transform error (XSLT failure) |

### Config File (`crucible.yaml`)

Optional config file in project root. CLI flags override config values.

```yaml
# Crucible documentation site configuration
title: My Documentation
base-url: /
source: ./docs
output: ./dist
# theme: ./my-theme
# extensions:
#   - Crucible.Extensions.Mermaid
```

### Example Workflows

```bash
# Standard build
crucible build -s ./docs -o ./dist --base-url https://docs.phoenixmldb.com

# Debug the XML intermediate
crucible build -s ./docs -o ./intermediate --stage parse
# Inspect XML, then:
crucible build -s ./intermediate -o ./dist --stage transform

# Local preview (future)
crucible serve -s ./docs
```

## Error Handling

### Build Behavior

- **Fail-fast by default on structural errors**: missing `title` in frontmatter, malformed YAML, XSLT transformation failures all cause the build to stop with a clear error message referencing the source `.md` file and line number where possible.
- **Warnings for non-fatal issues**: broken internal links, missing images, unrecognized frontmatter fields. Warnings are emitted to stderr and do not stop the build unless `--strict` is used.
- **Collect-then-report**: when multiple files have errors, the parser collects all parse errors and reports them together rather than stopping at the first one.

### Edge Cases

- **Empty source directory**: produces an empty site with only `sitemap.xml` — not an error
- **Missing frontmatter**: `title` is required; if absent, emit an error for that file and skip it. All other fields have sensible defaults.
- **No body content**: valid — produces a page with just the title and metadata
- **Directory with no `index.md`**: appears as a section in navigation but has no landing page. The section title is inferred from the directory name (title-cased).
- **File permission errors**: reported as errors, build continues with remaining files

### Diagnostics

- `--verbose`: logs each file being processed, stage transitions, extension invocations, XSLT template matches
- `--timing`: per-stage and per-file timing breakdown
- XSLT errors include the source document path and the XSLT template/line that failed

## Link Resolution

Internal links between documentation pages are resolved during Stage 1:

- **Relative paths** (e.g., `[Install](../getting-started/installation.md)`) — resolved relative to the current file's location. These work in Markdown editors and GitHub rendering.
- **Root-relative paths** (e.g., `[Install](/getting-started/installation.md)`) — resolved from the source directory root. Useful for deeply nested pages that would otherwise need long `../../..` chains.
- Both forms are rewritten from `.md` to `.html` in the XML output
- Broken internal links (referencing non-existent `.md` files) emit a warning
- Anchor links to headings (e.g., `installation.md#requirements`) are preserved and validated against known heading IDs
- External links (`http://`, `https://`) are passed through unchanged

## Markdown Extensions

Crucible uses Markdig with these extensions enabled:

- **YAML frontmatter** — via Markdig's `Yaml` extension
- **Tables** — pipe tables (GFM-style)
- **Fenced code blocks** — with language info strings
- **Admonitions** — using Markdig's custom container syntax:
  ```markdown
  ::: note
  This is a note admonition.
  :::
  ```
  Supported types: `note`, `warning`, `tip`, `important`, `caution`
- **Task lists** — GFM-style checkboxes
- **Auto-links** — bare URLs converted to links
- **Emoji** — `:emoji_name:` shortcodes (optional, can be disabled)

## SEO Strategy

Every page includes:
- `<title>` — from frontmatter `title` + site title
- `<meta name="description">` — from frontmatter `description`
- `<link rel="canonical">` — from `base-url` + page path
- Open Graph tags (`og:title`, `og:description`, `og:url`, `og:type`)
- Semantic HTML5 landmarks for accessibility and search indexing
- Auto-generated `sitemap.xml` with `lastmod` dates

## Testing Strategy

- **Crucible.Core.Tests**: Unit tests for Markdown → XML transformation, frontmatter parsing, site manifest generation, AST walker
- **Crucible.Extensions.Tests**: Unit tests for Mermaid extension (AST claiming, XML output, asset generation)
- **Integration tests**: End-to-end builds with sample documentation directories, verifying HTML output structure and content
- **XSLT template tests**: Verify default theme produces valid, semantic HTML5 for each document element type

## Scoping Notes

- **v1 does full rebuilds only** — incremental/watch builds are a future enhancement
- **Search is not in v1 scope** — `theme.js` includes mobile nav toggle only. Client-side search (e.g., lunr.js with a pre-built index) is a natural v2 feature
- **`crucible serve` is optional for v1** — if included, it's a simple static file server with file-watch rebuild; if deferred, `--watch` can be added later
- **Sitemap XSLT is not overridable by themes in v1** — it follows the sitemaps.org protocol strictly. Theme override of sitemap generation can be added if a real use case emerges
