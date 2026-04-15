# `modern` theme — design

Date: 2026-04-15
Status: Approved, ready for implementation plan

## Goal

Ship a second built-in Crucible theme, `modern`, that mirrors the visual language of cursor.com/docs: three-column shell, cool-neutral slate palette with a blue accent, ⌘K search overlay, and dark code blocks with filename tab, copy button, and client-side syntax highlighting. The existing `default` theme stays the implicit default; users opt into `modern` via `crucible build --theme modern` or `theme: modern` in `crucible.yaml`.

## File layout

```
src/Crucible.Core/Themes/modern/
  page.xslt            # 3-column layout
  sitemap.xslt         # identical to default
  css/
    style.css          # slate palette, 3-col grid, dark code blocks, admonitions
    prism.css          # Prism "tomorrow-night", bundled locally
  js/
    search.js          # ⌘K overlay (Lunr-backed)
    theme.js           # dark-mode toggle (same contract as default)
    toc.js             # scroll-spy for right-side "On this page"
    copy.js            # copy-button handler for <pre><code>
    prism.js           # Prism core + autoloader
```

`ThemeLoader.GetStaticAssets` already copies the contents of `css/` and `js/` recursively, so no loader change is needed for asset discovery. The `Crucible.Core.csproj` content glob must include `Themes/modern/**/*`; broaden the existing glob to `Themes/**/*` if it currently names `default` explicitly.

## Layout (page.xslt)

### Header

- Logo linking to `index.html`
- Theme toggle button (reuses `theme.js` contract — `data-theme` on `<html>`, `crucible-theme` in localStorage)
- ⌘K search button (opens overlay; shows `⌘K` / `Ctrl K` hint)
- Nav-drawer toggle button (mobile only)

No product-level nav links (no "Docs / Blog / Pricing"). The theme targets docs-only sites.

### Body grid

CSS grid with three tracks:

| Track       | Width              | Collapses at |
| ----------- | ------------------ | ------------ |
| Left nav    | 240px              | ≤ 768px (becomes slide-in drawer) |
| Main        | min 0, max 720px   | — |
| Right TOC   | 220px              | ≤ 1280px (hidden entirely) |

- **Left nav (`<aside class="sidebar">`)** — walks the `<site>` manifest (same XSLT logic as `default`, restyled). Section titles become small uppercase labels; pages are flush-left links with a colored left border on `.active`.
- **Main (`<main><article>`)** — eyebrow (parent section title from manifest), H1 (from `document/@title`), subtitle (from `document/@description`), then `<xsl:apply-templates select="body/*"/>`.
- **Right TOC (`<aside class="toc">`)** — generated at XSLT time from `//heading[@level = ('2','3')]`. See §"Right-side TOC" below.

### Footer

Single-line credit, identical to `default`.

## Visual style (style.css)

### Palette

| Token                | Light      | Dark       |
| -------------------- | ---------- | ---------- |
| `--bg`               | `#ffffff`  | `#0b0f19`  |
| `--bg-subtle`        | `#f8fafc`  | `#111827`  |
| `--fg`               | `#0f172a`  | `#e5e7eb`  |
| `--fg-muted`         | `#64748b`  | `#94a3b8`  |
| `--border`           | `#e2e8f0`  | `#1e293b`  |
| `--accent`           | `#2563eb`  | `#60a5fa`  |
| `--accent-subtle`    | `#eff6ff`  | `#1e3a8a33` |
| `--code-bg`          | `#0b0d12`  | `#0b0d12`  (same) |
| `--code-fg`          | `#d1d5db`  | `#d1d5db`  (same) |

Dark mode activation matches `default`: `[data-theme="dark"]` override + `@media (prefers-color-scheme: dark)` for users who haven't toggled.

### Typography

- **Body font**: system stack (`-apple-system, BlinkMacSystemFont, "Segoe UI", Inter, Roboto, "Helvetica Neue", Arial, sans-serif`)
- **Mono font**: `"SFMono-Regular", Consolas, "Liberation Mono", Menlo, Courier, monospace`
- **Sizes**: body 16px / line-height 1.7, H1 30px / 600, H2 22px / 600, H3 18px / 600, inline code 14px
- **Content max-width**: 720px

### Links

- Accent color, no underline by default
- In-prose `<a>` inside `<article>` gets a subtle 1px bottom border that thickens on hover
- Sidebar / TOC links have no underline, rely on color and indent

### Admonitions

Colored 3px left border + icon + tight title. One CSS block per type, mapping to the existing `<admonition type="note|tip|warning|danger">` element:

| Type     | Accent color | Icon |
| -------- | ------------ | ---- |
| note     | blue         | ℹ    |
| tip      | green        | ✓    |
| warning  | amber        | ⚠    |
| danger   | red          | ⛔   |

## Code blocks

### Rendered shape

```html
<figure class="code">
  <figcaption><span class="filename">install.sh</span><span class="lang">bash</span></figcaption>
  <pre><code class="language-bash">dotnet tool install -g Crucible.Cli</code></pre>
  <button class="copy" type="button" aria-label="Copy code">Copy</button>
</figure>
```

`<figcaption>` is omitted entirely when there is no filename and no language.

### Filename source

Markdig stores the fenced info string verbatim in `FencedCodeBlock.Info`. Today `MarkdownToXmlEmitter.EmitFencedCodeBlock` writes it whole into `@language`, which breaks as soon as the author writes anything past the language token.

Change the emitter to split the info string into two attributes:

- `` ```bash `` → `<code-block language="bash">`
- `` ```bash title="install.sh" `` → `<code-block language="bash" filename="install.sh">`

Parsing rule: take everything up to the first whitespace as `@language`; if the remainder matches `title="…"` (or `title='…'`), use the quoted value as `@filename`. Unknown trailing text is discarded. This is theme-agnostic — `default/page.xslt` keeps rendering `@language` as it does today and ignores the new `@filename`.

### Highlighting

Prism bundled locally as a single pre-built `js/prism.js` file that includes core plus a fixed set of common languages — `markup`, `css`, `javascript`, `typescript`, `json`, `yaml`, `bash`, `shell`, `csharp`, `xml`, `diff`, `markdown`, `python`. Paired with `css/prism.css` using the "tomorrow-night" theme. No autoloader, no CDN — everything ships in the theme. Unsupported languages fall back to un-highlighted monospace text; adding a language is a matter of regenerating the Prism bundle.

Code blocks always use the dark background regardless of light/dark page mode — this matches cursor.com/docs and avoids theme-mismatched highlighting.

### Copy button

`copy.js` attaches a click handler that reads `textContent` of the sibling `<code>` element and writes to `navigator.clipboard.writeText`. Button text swaps to "Copied" for 1.5s after success.

## Search (⌘K overlay)

### Reuses existing index

`SearchIndexBuilder` already emits `search-index.json`. `TransformStage` already copies it to `dist/`. No pipeline changes.

### Behavior

- **Open**: ⌘K (macOS) / Ctrl+K (other) / `/` anywhere; click the header search button.
- **Close**: Esc or click on the overlay backdrop.
- **Navigate**: ↑ / ↓ to move highlight, Enter to open, Tab/Shift+Tab also work.
- **Index**: built lazily on first open from `search-index.json` + Lunr. Cached for the session.

### UI

Centered panel, ~600px wide, over a dimmed backdrop. Input at top; results below as a scrollable list. Each result: page title (bold), breadcrumb path (muted), snippet (~140 chars of surrounding content). Empty state when no query. "No results" state when the query has zero hits.

Lunr loaded from the unpkg CDN (same as `default`) to keep the theme asset footprint small. Bundling Lunr is a follow-up task, not part of this spec.

## Right-side TOC

### XSLT generation

A dedicated template walks `//heading[@level = ('2','3')]` in document order. Anchor IDs already exist on `<heading>` elements (`SlugGenerator` assigns them during parse). The emitted markup:

```xml
<aside class="toc" aria-label="On this page">
  <p class="toc-label">On this page</p>
  <ul>
    <li class="toc-h2"><a href="#section-id">Section title</a></li>
    <li class="toc-h3"><a href="#subsection-id">Subsection title</a></li>
  </ul>
</aside>
```

Indentation of H3s is handled in CSS (`li.toc-h3 { padding-left: 12px }`).

### Opt-out

Frontmatter key `toc: false` skips rendering the aside. Implementation:

1. Add `bool? Toc { get; set; }` to `DocumentMetadata`
2. `MarkdownToXmlEmitter` writes `toc="false"` on `<document>` only when the metadata value is explicitly `false`
3. `modern/page.xslt` checks `document/@toc != 'false'` before emitting the aside
4. `default/page.xslt` ignores the attribute

### Scroll-spy

`toc.js` uses `IntersectionObserver` on all `h2[id], h3[id]` in `<article>`. The most recently intersected heading's corresponding TOC link gets `.active`.

## Changes to existing code

All four are additive. `default` theme renders identically before and after.

1. **`Crucible.Core/Parsing/MarkdownToXmlEmitter.cs`** — `EmitFencedCodeBlock` splits `fenced.Info` on first whitespace; parses optional `title="…"` / `title='…'` as `@filename`.
2. **`Crucible.Core/Models/DocumentMetadata.cs`** — add `bool? Toc { get; set; }`.
3. **`Crucible.Core/Parsing/MarkdownToXmlEmitter.cs`** — when emitting `<document>`, if `metadata.Toc == false`, write `toc="false"` attribute.
4. **`Crucible.Core/Themes/ThemeLoader.cs`** — if `customThemePath` is a non-empty string that is not a directory but matches a folder name under `AppContext.BaseDirectory/Themes/`, resolve it as a built-in. This enables `--theme modern` without a path. Pass-through today's behaviour when the value is `null` or a real directory.
5. **`src/Crucible.Core/Crucible.Core.csproj`** — broaden the `Themes` content glob to include `Themes/**/*` so the new theme's assets ship.

## Testing

### Emitter unit tests (new)

- `EmitFencedCodeBlock` with info `"bash"` → `@language="bash"`, no `@filename`
- …with info `"bash title=\"install.sh\""` → `@language="bash"`, `@filename="install.sh"`
- …with info `"bash title='install.sh'"` → same as above
- …with info `"plaintext extra-junk"` → `@language="plaintext"`, no `@filename`
- `<document>` emission with `Toc = false` → `@toc="false"` present
- `<document>` emission with `Toc = null` or `Toc = true` → no `@toc` attribute

### ThemeLoader unit tests (new)

- `new ThemeLoader(null)` → resolves `default` as today
- `new ThemeLoader("modern")` → resolves the `modern` built-in
- `new ThemeLoader("/absolute/path/to/custom")` → treats as directory, unchanged

### Build-integration test (new)

Against a small fixture doc tree, run the pipeline with `--theme modern` and assert against the resulting HTML:

- A page with a `## Section` heading produces a `<aside class="toc">` containing `<a href="#section">`
- A page with frontmatter `toc: false` produces no `<aside class="toc">`
- A fenced block ` ```bash title="install.sh" ` produces `<figcaption>` containing `install.sh`
- The header contains a `button` that opens the ⌘K overlay (presence check on class/ID)

### Manual validation

- Open `dist/` in a browser (light mode, dark mode, narrow viewport)
- Confirm: ⌘K opens overlay, scroll-spy highlights current heading, copy button flashes "Copied"
- Run the same fixture against `--theme default` and confirm no regressions

## Out of scope (follow-ups)

- Bundling Lunr locally instead of CDN
- Landing-page card-grid styling
- Versioned docs / multi-product sidebar
- Keyboard shortcut for jumping to sidebar sections
- Expanding the Prism language list beyond the bundled set
