# Releases

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
