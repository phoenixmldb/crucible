# Releases

## Unreleased

### Fixed
- **Modern theme: ⌘ K search returned no results, ever.** `search.js` expected
  `{ "documents": [...] }` but `search-index.json` is a top-level array, so lunr
  indexed nothing. It also read a `content` field that has never existed (the
  field is `body`) and ignored `description`/`headings`. Shipped broken in 1.1.45.
- **Both themes: search ignored `base-url`.** The default theme fetched
  `/search-index.json` (broke every subpath deploy — GitHub Pages project sites,
  anything under `/docs/`); the modern theme fetched it document-relative (broke
  on every nested page). Both now resolve against `window.CRUCIBLE_BASE`,
  injected by `page.xslt`. Result links were wrong in the same two ways.

- **Mermaid was included per diagram, and its init script never at all.** The
  runtime `<script>` lived in the per-diagram template, so a page with N
  diagrams pulled the same multi-megabyte CDN bundle N times, while the
  `mermaid-init.js` emitted by `Crucible.Extensions` was referenced by neither
  theme. Both themes now include the runtime and the init script exactly once
  per page, and only when the page actually has a diagram. The CDN URL is
  pinned to `mermaid@11.6.0` with an SRI hash; init drives `mermaid.run()`
  explicitly instead of relying on `startOnLoad` timing, and follows dark mode.
- **Prism could not highlight most documented languages.** The bundled build was
  core-only (markup, css, clike, javascript, json, python) — `csharp`, `bash`,
  `yaml`, `sql`, `typescript`, `powershell`, `docker`, `diff` and `markdown`
  all rendered unhighlighted. Rebuilt on Prism 1.30.0 with those grammars, plus
  `xslt`/`xsl` aliased to markup. Slightly smaller than the old bundle.
- **Packaging: `Themes/ThemeLoader.cs` shipped inside the published tool.** The
  `Themes/**/*` content glob swept up the C# source next to the theme folders.
  Now excludes `*.cs` and `*.md`.
- `ThemeLoader` gives a clear error naming the missing file when pointed at a
  directory that is not a theme, instead of failing inside `File.ReadAllText`.
- **Section landing pages were missing from `sitemap.xml`.** `SiteManifestBuilder`
  treats a subdirectory's `index.md` as section metadata and omits it from the
  manifest as a page — correct for navigation, but the sitemap is generated from
  that same manifest, so those pages were rendered, published, and invisible to
  crawlers. On a 112-page site that was 15 pages. `<section>` now carries an
  `index-path` attribute and the sitemap lists it.
- **URL construction is defined once.** Nav links, `canonical`/`og:url` and
  sitemap `<loc>` each built URLs inline, and `search.js` built its own in
  JavaScript. They are now all `c:page-url` from `Themes/_base/urls.xslt`
  (mirrored in `search.js`, pinned together by `LinkFormatTests`). URLs are
  extensionless and a trailing `index` segment collapses to its directory, so a
  landing page and the directory it indexes share one canonical URL:
  `guides/index` → `<base>guides/`.

- **Modern theme: code blocks rendered in a different font than inline code.**
  `prism.css` shipped base rules for `code[class*=language-]`, whose attribute
  selector outranked the theme's plain `code` rule — so code blocks used
  Prism's `Consolas, Monaco, …` stack at `line-height: 1.5`, while inline code
  used `--font-mono` at `1.6`. `prism.css` is now reduced to `.token.*` colors
  only, leaving `style.css` the single owner of code-block typography;
  `tab-size: 4` moved to `figure.code pre`, where the theme owns it.

  **Visible change:** code blocks now render in the theme's mono font at the
  theme's line height. No token rules were removed, so syntax colors are
  unchanged.

### Changed
- **Analytics is now opt-in and config-driven.** The modern theme hardcoded the
  Endpoint Systems GA4 property (`G-FSCPKZ7RES`) in 1.1.47, so every site built
  with `-t modern` reported traffic into it. Generated sites now emit no
  tracking unless `analytics.ga4` is set in `crucible.yaml`:
  ```yaml
  analytics:
    ga4: G-XXXXXXXXXX
  ```
  Supported by both built-in themes. `TransformStage.ExecuteAsync` takes a new
  optional `analytics` parameter before `ct`.
- **Shared theme XSLT.** `default/page.xslt` and `modern/page.xslt` duplicated
  ~140 lines of body-element templates, and their sitemaps were byte-identical.
  Both now `xsl:import` `Themes/_base/elements.xslt` and `_base/sitemap.xslt`;
  the modern theme overrides only `code-block` and `table`. Generated HTML is
  byte-for-byte unchanged. See `Themes/README.md`.
- **lunr is vendored, not fetched from a CDN.** Both themes loaded
  `https://unpkg.com/lunr/lunr.js` — unpinned, no SRI, and the sole dependency
  of search. Now served from the site as `js/lunr.js` (lunr 2.3.9), so search
  works offline and under a strict CSP.
- **The page stylesheet is compiled once per build**, not once per page.
  Measured on a 100-page site with the modern theme: ~4830ms → ~2970ms
  (~39%). Output is byte-identical.
- **Built-in theme names are validated.** A `--theme`/`theme:` value containing
  a path separator or `..`, or starting with `_`, is no longer resolved as a
  built-in name, so it cannot escape `Themes/` or select the shared `_base`
  fragment. Explicit directory paths are unaffected.
- Removed `default/navigation.xslt` — dead since it was written (nothing
  imported it) and drifted out of sync with the nav templates actually in use.
- The modern theme's `.nav-section.open` class, computed by `page.xslt` but
  styled by no rule, now highlights the section containing the current page.

### Dependencies
- `PhoenixmlDb.Xslt` `1.1.0.21` → `1.5.0` (no API changes required)
- `Markdig` `1.1.2` → `1.3.2`
- `YamlDotNet` `16.3.0` → `18.1.0`
- `Microsoft.NET.Test.Sdk` `17.12.0` → `18.8.1`
- `coverlet.collector` `8.0.1` → `10.0.1`
- `FluentAssertions` held at `6.12.2` — 7.x/8.x moved to the Xceed license
  (paid for commercial use); 6.12.2 is the last MIT release.

### NuGet
- **`crucible.cli` is now the only published package.** `Crucible.Core` and
  `Crucible.Extensions` are `IsPackable=false`; both ship *inside* the tool,
  which `PackAsTool` builds from the full publish output.

  `crucible.extensions` was published from `1.0.0` through `1.1.47` and **every
  one of those versions is unrestorable**. Its `ProjectReference` to
  `Crucible.Core` became a hard NuGet dependency on a package CI never packed:

  ```
  error: NU1101: Unable to find package Crucible.Core.
  error: Package 'crucible.extensions' is incompatible with 'all' frameworks
  ```

  `crucible.cli` was never affected. Download counts across those versions are
  flat (~100 each) despite being impossible to install, which is mirror and
  scanner traffic rather than consumers — no evidence anything used it as a
  library.

  Publishing the libraries properly would mean committing to a public API
  surface, and for `Crucible.Core` also shipping `Themes/` as package content,
  since `ThemeLoader` resolves built-in themes from `AppContext.BaseDirectory`.
  That is a deliberate decision to make later, not a side effect of fixing a
  broken dependency. A CI guard fails the build if anything other than
  `crucible.cli` appears in the pack output.

  The stale `crucible.extensions` versions remain listed on nuget.org; unlisting
  them is a separate manual step.

### CI / publish flow
- **Switched to NuGet trusted publishing.** The publish job now exchanges a
  GitHub OIDC token for an API key valid for one hour, via `NuGet/login@v1`,
  instead of the long-lived `NUGET_API_KEY` secret.

  That secret stopped working roughly 90 days after the last release, with no
  warning: every push returned `403 (The specified API key is invalid, has
  expired, or does not have permission…)`, including for the long-existing
  `crucible.cli`. Short-lived credentials remove key expiry as a release
  blocker, and there is no standing secret to leak.

  Requires a trusted publishing policy on nuget.org matching repository owner
  `phoenixmldb`, repository `crucible`, workflow file `ci.yml`, no environment.
  **Renaming this workflow file breaks the policy.** The nuget.org profile name
  is the `NUGET_USER` repository variable — a variable rather than a secret,
  since it is public and correcting it should not need a commit.

---

## 1.1.47 — 2026-04-28

Published 02:32 UTC (`86b1c3f`).

### Added
- **Modern theme: Google Analytics 4 tag.** A hardcoded `G-FSCPKZ7RES` gtag
  snippet in `modern/page.xslt`, added for phoenixml.dev.

  This was the only change in the release; the packaged payload differs from
  1.1.46 by exactly those eight lines. It was intentional at the time ("only one
  site uses this theme today") but meant every site built with `-t modern`
  reported into the Endpoint Systems property. Replaced by opt-in
  `analytics.ga4` configuration — see Unreleased.

---

## 1.1.46 — 2026-04-28

Published 00:58 UTC (`5d5d07a`). **No functional change.**

A documentation-only commit syncing `RELEASES.md` with the 1.1.45 publish.
Because CI publishes on every push to `main`, it produced a NuGet release whose
payload is byte-identical to 1.1.45 apart from version stamping — the shipped
`Themes/` tree diffs clean against 1.1.45.

Nothing to upgrade for. Noted so the version sequence has no unexplained holes.

---

## 1.1.45 — 2026-04-28

First public release of the **modern theme** — a cursor.com/docs-style three-column layout for documentation sites.

### Added
- **Modern theme** (built-in, opt-in via `theme: modern` in `crucible.yaml`):
  - Three-column layout: sidebar nav, content, right-side TOC
  - Dark-mode toggle with `localStorage` persistence
  - ⌘ K command-palette search overlay
  - Scroll-spy active-section highlighting in the right TOC
  - Copy-to-clipboard button on code blocks
  - Bundled Prism.js (v1.29.0, "Tomorrow" palette) for syntax highlighting
  - Mobile nav-drawer toggle
- **Parser:** `toc: false` frontmatter flag to opt out of right-side TOC per page
- **Parser:** filename extraction from fenced code-block info string (e.g. ` ```ts:src/foo.ts `)
- **ThemeLoader:** built-in theme name resolution — `theme: default` and `theme: modern` resolve without a path

### Changed
- `PhoenixmlDb.Xslt` bumped to `1.1.0.21`
- `coverlet.collector` bumped to `8.0.1`
- CI version scheme: `1.0.${commit_count}` → `1.1.${commit_count}` to align with semver intent

### Fixed
- Default theme footer emitted invalid XML attributes (`noreferrer noopener` as boolean attrs); now correctly uses `rel="noopener noreferrer"`. This unblocks `Crucible.Core.Tests.Pipeline.{TransformStageTests,EndToEndTests}` which had been failing CI since 2026-03-31.

### NuGet
- `crucible.cli` — `1.0.0` → `1.1.45`. Note: the `52d60f7` rename to package id `crucible` was reverted in `a552432` because `crucible` is owned by an unrelated publisher (PrimS) on NuGet. Tool command name remains `crucible` for end users.
- `crucible.extensions` — `1.0.0` → `1.1.45`. (Also includes `1.1.44` from a prior partial publish.)

### CI / publish flow
- Migrated from NuGet trusted-publisher auth to API-key auth (`NUGET_API_KEY` GitHub secret). Trusted-publisher policy matching had been a recurring deployment obstacle (creator-vs-owner mismatch).
- Removed `id-token: write` permission from publish job (no longer needed).

### Known issues
- Default theme has not been re-validated against all 100+ pages of phoenixml.dev since the footer fix; spot-check before relying on it.

---

## 1.0.0 / 0.1.0-alpha — pre-2026-04

Pre-release history not tracked here. Highlights: initial scaffold (`crucible`, `crucible.extensions`, `crucible.core`), CI on GitHub Actions, NuGet trusted-publisher publish flow, package id rename `crucible.cli` → `crucible`.
