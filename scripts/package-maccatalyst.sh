#!/usr/bin/env bash
#
# Package the Mac Catalyst app for distribution outside the Mac App Store: publish, sign
# with a Developer ID certificate, notarise, staple.
#
# This is a local script rather than a CI job on purpose. Notarisation fails in opaque ways
# and each attempt through CI costs minutes; on your own Mac the loop is seconds and the
# error messages are in front of you. Once this runs clean, lifting it into release.yml is
# mechanical — the commands do not change, only where the certificates come from.
#
# Prerequisites, all one-time:
#
#   1. An Apple Developer Program membership.
#
#   2. Two certificates in your login keychain, both downloadable from
#      https://developer.apple.com/account/resources/certificates :
#        - "Developer ID Application" — signs the .app
#        - "Developer ID Installer"   — signs the .pkg
#      List what you have with:  security find-identity -v
#
#   3. A stored notarytool credential, so this script never handles your password:
#        xcrun notarytool store-credentials words-notary \
#          --apple-id you@example.com \
#          --team-id YOURTEAMID \
#          --password <an app-specific password from appleid.apple.com>
#
# Usage:
#
#   CODESIGN_KEY="Developer ID Application: Your Name (TEAMID)" \
#   PACKAGE_KEY="Developer ID Installer: Your Name (TEAMID)" \
#   scripts/package-maccatalyst.sh 0.1.0
#
# The version argument is optional; without it the version in the csproj is used.

set -euo pipefail

version="${1:-}"
profile="${NOTARY_PROFILE:-words-notary}"
project="src/Words.Maui"
framework="net10.0-maccatalyst"
outdir="publish/maccatalyst"

fail() { echo "error: $*" >&2; exit 1; }

[ -n "${CODESIGN_KEY:-}" ] || fail "CODESIGN_KEY is not set. Run: security find-identity -v"
[ -n "${PACKAGE_KEY:-}" ] || fail "PACKAGE_KEY is not set. Run: security find-identity -v"
command -v xcrun >/dev/null || fail "xcrun not found — Xcode command line tools are required."

echo "==> Publishing $framework"

# Both architectures. A Developer ID build is downloaded by strangers on hardware you cannot
# predict, so an arm64-only package would simply fail to launch on an Intel Mac.
#
# UseHardenedRuntime is not optional: notarisation rejects anything without it.
publish_args=(
  "$project"
  -c Release
  -f "$framework"
  -p:RuntimeIdentifiers="maccatalyst-x64;maccatalyst-arm64"
  -p:CreatePackage=true
  -p:EnableCodeSigning=true
  -p:EnablePackageSigning=true
  -p:UseHardenedRuntime=true
  -p:CodesignKey="$CODESIGN_KEY"
  -p:PackageSigningKey="$PACKAGE_KEY"
)
[ -n "$version" ] && publish_args+=(-p:ApplicationDisplayVersion="$version")

dotnet publish "${publish_args[@]}"

# MAUI writes the .pkg under the framework's own output tree rather than anywhere we choose,
# and the exact path has moved between SDK versions — so find it rather than assume it.
pkg=$(find "$project/bin/Release/$framework" -name '*.pkg' -print | head -1)
[ -n "$pkg" ] || fail "No .pkg was produced. Look under $project/bin/Release/$framework for what was."

mkdir -p "$outdir"
target="$outdir/words${version:+-$version}-maccatalyst.pkg"
cp "$pkg" "$target"
echo "==> Built $target"

echo "==> Notarising (this waits for Apple, usually a few minutes)"
# --wait blocks until Apple accepts or rejects. On rejection, ask for the log:
#   xcrun notarytool log <submission-id> --keychain-profile "$profile"
xcrun notarytool submit "$target" --keychain-profile "$profile" --wait

echo "==> Stapling"
# Staples the notarisation ticket into the package so Gatekeeper accepts it offline. Without
# this a first launch with no network is refused.
xcrun stapler staple "$target"

echo "==> Verifying"
xcrun stapler validate "$target"
spctl --assess --type install -vv "$target" || echo "note: spctl assessment above is advisory for a .pkg"

echo
echo "Done: $target"
echo "Attach it to the GitHub release for the matching tag:"
echo "  gh release upload v${version:-X.Y.Z} \"$target\""
