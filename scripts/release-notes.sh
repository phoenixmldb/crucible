#!/usr/bin/env bash
# Extract the RELEASES.md section for a given version, for PackageReleaseNotes.
#
#   scripts/release-notes.sh 1.1.63 [RELEASES.md]
#
# Prints the body of the first "## " section whose heading names the version, up
# to the next "## " heading. Because CI publishes one version per commit, a
# heading may cover a RANGE ("## 1.1.48 - 1.1.62 - ..."); a version inside such a
# range matches it, so every published version resolves to the notes describing
# the work it contains.
#
# Fails closed: an unmatched version exits non-zero rather than packing a release
# with empty notes.
set -euo pipefail

VERSION="${1:?usage: release-notes.sh <version> [releases-file]}"
RELEASES="${2:-$(dirname "$0")/../RELEASES.md}"

if [ ! -f "$RELEASES" ]; then
  echo "release-notes: no such file: $RELEASES" >&2
  exit 1
fi

notes=$(
  awk -v version="$VERSION" '
    function vnum(v,   a) {
      split(v, a, ".")
      return (a[1] * 1000000) + (a[2] * 1000) + a[3]
    }
    # Heading lines start a new section. Decide whether this is the one.
    /^## / {
      if (found) exit                       # next heading ends our section

      # Collect the X.Y.Z tokens in the heading. Dates use "-" and so never
      # look like versions.
      n = 0
      rest = $0
      while (match(rest, /[0-9]+\.[0-9]+\.[0-9]+/)) {
        v[++n] = substr(rest, RSTART, RLENGTH)
        rest = substr(rest, RSTART + RLENGTH)
      }

      found = 0
      if (n == 1) {
        found = (v[1] == version)           # single version: exact match
      } else if (n >= 2) {                  # range heading: numeric containment
        lo = vnum(v[1]); hi = vnum(v[2])
        if (lo > hi) { t = lo; lo = hi; hi = t }
        t = vnum(version)
        found = (t >= lo && t <= hi)
      }
      next
    }
    found { print }
  ' "$RELEASES"
)

# Trim leading/trailing blank lines and the trailing "---" separator.
notes=$(printf '%s\n' "$notes" | sed -e 's/[[:space:]]*$//' -e '/^---$/d')
notes=$(printf '%s\n' "$notes" | sed -e '/./,$!d' | tac | sed -e '/./,$!d' | tac)

if [ -z "$notes" ]; then
  echo "release-notes: no section in $RELEASES names version $VERSION" >&2
  echo "  add a '## $VERSION - <date>' heading, or extend an existing range heading." >&2
  exit 1
fi

printf '%s\n' "$notes"
