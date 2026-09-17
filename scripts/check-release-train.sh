#!/usr/bin/env bash
# Refuses to pack a release whose engine pins are not the release version.
#
# This is the check that would have stopped the artifact Martin Honnen was given. The published
# PhoenixmlDb.Xslt 1.6.13 depends on PhoenixmlDb.XQuery 1.6.12, because the XSLT tag was cut
# before the XQuery 1.6.13 push. Nothing was "wrong" at pack time — the pin was the latest
# published version — so a staleness check cannot catch it. Only an equality check can:
#
#   if this train is 1.6.14, every train-locked pin must read 1.6.14.
#
# Why this repo needs it. crucible.cli EMBEDS the XSLT engine — PackAsTool ships the full
# publish output — and crucible builds phoenixml.dev. So the pin below decides which engine
# renders our own documentation. It had been sitting on PhoenixmlDb.Xslt 1.6.13, the one version
# recorded as known-bad (it ships depending on XQuery 1.6.12; see phoenixmldb-xslt
# docs/RELEASE-HYGIENE.md, Tier 1), and nothing noticed because this repo had no pin check of
# any kind.
#
# Mark a pin with "check-pins: train-locked" in the comment above it to opt in. Packages on
# their own cadence (Core) are deliberately not locked; scripts/check-pins.sh still stops those
# from falling behind what is published.
#
# WHAT "THE TRAIN" MEANS HERE, AND WHY IT IS NOT THIS REPO'S VERSION.
#
# This script was ported verbatim from phoenixmldb-xslt, where the repo BEING released is itself
# a train member: Core, XQuery and Xslt move to 2.1.0 together, so "every train-locked pin equals
# my own release version" is a meaningful rule.
#
# crucible is not a train member. It is a CONSUMER on its own 1.1.x line. Passing crucible's
# release version here asked whether PhoenixmlDb.Xslt was pinned at "1.1.77" — a version that
# will never exist — so the gate could only ever FAIL, and every crucible tag was unreleasable.
# That is the inverse of the defect this project keeps finding (a check that cannot fail); a
# check that cannot pass is just as useless, and it hid behind never having cut a tag.
#
# The operand is therefore the ENGINE train crucible targets, declared as <EngineTrain> in
# Directory.Packages.props next to the pins it governs.
#
# Be clear about what that buys today: crucible has exactly ONE train-locked pin, so this
# compares that pin against a number written three lines above it. It catches a pin bumped
# without the declaration moving with it, and it generalises correctly the moment a second
# engine pin appears. It is NOT what would have caught PhoenixmlDb.Xslt 1.6.13 sitting here for
# three releases — that was staleness, and check-pins.sh is the check for it.
set -uo pipefail

version="${1:-}"
props="${2:-Directory.Packages.props}"
[ -n "$version" ] || { echo "usage: check-release-train.sh <engine-train-version> [props]"; exit 2; }
version="${version#v}"
[ -f "$props" ] || { echo "check-release-train: no $props here"; exit 2; }

# Which pins are train-locked. A marker locks the NEXT PhoenixmlDb PackageVersion line and
# nothing else. This used to be `grep -B8` above each pin, which also locked any pin landing
# within eight lines AFTER a marker — so adding a pin below a locked one would silently lock it
# too, and the release would then fail on a package that runs its own cadence. Anchoring to the
# following line removes a trap that only fires when someone edits this file months from now.
locked_ids=$(awk '
  /check-pins: train-locked/ { pending = 1 }
  /<PackageVersion Include="PhoenixmlDb[^"]*"/ {
    if (pending) { match($0, /Include="[^"]+"/); print substr($0, RSTART + 9, RLENGTH - 10); pending = 0 }
  }
' "$props")

fail=0 locked=0
while read -r id ver; do
  if ! printf '%s\n' "$locked_ids" | grep -qx -- "$id"; then
    echo "free  $id $ver (not train-locked)"
    continue
  fi
  locked=$((locked+1))
  if [ "$ver" = "$version" ]; then
    echo "ok    $id $ver == train $version"
  else
    echo "FAIL  $id is pinned $ver but this train is $version"
    echo "      Packing now ships a tool whose engine is not the one this release tested."
    fail=1
  fi
done < <(grep -oE '<PackageVersion Include="(PhoenixmlDb[^"]*)" Version="([^"]+)"' "$props" \
         | sed -E 's/.*Include="([^"]+)" Version="([^"]+)".*/\1 \2/')

if [ "$locked" -eq 0 ]; then
  echo "check-release-train: no train-locked pins found — mark them with 'check-pins: train-locked'"
  exit 2
fi
exit $fail
