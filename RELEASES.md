# Releases

CI publishes every push to `main`, and the version is `1.1.${commit_count}`, so
there is one NuGet version per commit. Headings below therefore cover a *range*
of published versions where a body of work landed over several commits; the
highest version in the range is the first one containing all of it.

## Unreleased

_Nothing yet._

---

## 1.1.64 – 1.1.73 — 2026-08-03

A pass over the findings from the 2026-08-01 code review. Several of these change
what a build *reports*, and three can fail a build that previously passed — see
"Behaviour changes" below before upgrading a pipeline.

### Fixed

- **Documents that failed to parse were dropped from search and `llms.txt` with no
  warning.** Both generators caught every exception per document and skipped the
  file silently. The failure reached users as a search box that could not find
  pages visibly present on the site, with nothing in the build output to explain
  it. Both now report the file and the reason, and the bare `catch` narrows to the
  exception types actually expected, so anything else surfaces instead of being
  mistaken for a malformed file.

  Also covers the non-throwing path: a well-formed file whose root element was not
  `<document>` was dropped by a bare `continue`, producing the identical symptom
  without an exception ever being raised.

- **The 1.1.63 search-index fix was incomplete.** It decided staleness by comparing
  modification times with a strict `>`, which misses two cases that both ship an
  index short of real pages: a document injected inside the index's timestamp
  granularity *ties* rather than exceeding, and a file copied in with its
  modification time preserved is *older* than the index that predates it. The
  second is the likelier one in practice, since injecting generated pages is the
  documented extension point and copying them is the obvious way to do it.

  Staleness is now a coverage question — documents on disk against entries in the
  index — with the time comparison kept for content changes that leave the count
  unchanged, and ties counted as stale.

- **Plugins with dependencies loaded and then failed on first use.** The loader
  used a bare `AssemblyLoadContext` with no dependency resolution, so any plugin
  with a dependency threw `FileNotFoundException` at the first call into it, deep
  enough to obscure the cause. Resolution now runs through the plugin's
  `.deps.json`, which means **a plugin is a published output folder, not a loose
  `.dll`**.

- **Malformed frontmatter failed the build without naming the file.** A raw
  `YamlException` reported a line number inside the frontmatter block and nothing
  about which document it came from. Errors now carry the document path and a
  document-relative line, with the original diagnostic kept as the inner
  exception, and one bad file is a build error rather than an unattributed stack
  trace that takes the run down.

- **`--strict` did nothing.** It was parsed, stored, and read by nothing, while
  `--help` advertised "Treat warnings as errors" — so a CI pipeline passing it got
  a green build no matter what was reported.

- **`--verbose` did nothing.** Same shape. It now reports the input directory and
  the type it was detected as, the output directory, and the intermediate
  directory.

- **Library code resumed on the caller's synchronization context.**
  `Crucible.Core` used `ConfigureAwait(true)` throughout. Harmless under the CLI
  and ASP.NET Core, neither of which installs a context, but a responsiveness and
  deadlock hazard the first time the library is embedded in WPF, WinForms, or
  MAUI.

### Behaviour changes

- **`--strict` now fails builds.** This is the point of the fix, but a pipeline
  that has been passing `--strict` and going green may start failing. The warnings
  it escalates were always being printed.

- **A malformed closing delimiter is now "missing frontmatter" rather than a
  mangled page.** The terminator was previously any line *starting* with `---`, so
  a `----` rule or a typo ended the block early and leaked the remainder into the
  rendered body. It must now be a line that is exactly `---`, ignoring trailing
  whitespace. Documents relying on the old behaviour will now report an error
  instead of rendering incorrectly.

- **A skipped draft is information, not a warning.** It moved to a new `Messages`
  channel, printed as `info:` and never escalated by `--strict`, because a draft is
  the author's intent rather than a defect — escalating it would make `--strict`
  unusable on any site with work in progress. Anything parsing build output for
  `warning:` lines will no longer see drafts there.

- **`--verbose` keeps the intermediate directory.** A full build parses into a
  temporary directory and deletes it on the way out, so reporting that path and
  then removing it would not have been diagnostics. Under `--verbose` the
  directory is left in place for inspection, and is not cleaned up afterwards.

- **Plugin load contexts are no longer collectible.** They never were unloaded, so
  the flag cost indirection and reduced JIT optimization for a capability nothing
  used. No effect unless a host was relying on plugin assemblies being
  collectible, which nothing in-tree was.

---

## 1.1.63 — 2026-07-31

### Fixed
- **Generated pages added between build stages were missing from site search.**
  `ParseStage` builds `search-index.json`; `TransformStage` only copied it. A
  `--stage TransformOnly` run against a directory the caller had added documents
  to after `--stage ParseOnly` — the documented extension point for generated
  content — therefore shipped a search index that omitted them, silently and
  with no warning.

  phoenixml.dev hit this: its build parses Markdown, generates 137 API reference
  pages into the intermediate directory, then transforms. The site had 249 pages
  and a 112-document search index, so its entire generated API reference was
  unsearchable.

  `TransformStage` now rebuilds the index when any intermediate XML document is
  newer than it. A `Full` build is unaffected — parse writes the index last, so
  nothing is newer and the existing index is copied as before.

### Packaging
- **The package now carries release notes.** `PackageReleaseNotes` was never set,
  so every `crucible.cli` version through 1.1.62 shipped with an empty
  `<releaseNotes>` — nuget.org showed nothing about what changed. CI now extracts
  this file's section for the version being published (`scripts/release-notes.sh`)
  and the pack reads it in.

  Fails closed on the publish path: no matching section, or an empty one, aborts
  the pack rather than releasing without notes. Pull-request builds pack without
  notes, since a PR's commit count is not the version it will land as.

---

## 1.1.48 – 1.1.62 — 2026-07-28 → 2026-07-29

Published across 15 commits (`b743bda` … `e518525`). **1.1.62 is the first
version containing all of the below**; the intermediate versions each carry only
the work committed up to that point.

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
  `analytics.ga4` configuration in 1.1.49.

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
