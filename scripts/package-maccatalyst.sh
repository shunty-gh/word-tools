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

command -v xcrun >/dev/null || fail "xcrun not found — Xcode command line tools are required."

# The certificates have to be *Developer ID* ones. An "Apple Development" or "Apple
# Distribution" certificate signs without complaining and then fails notarisation minutes
# later, so the wrong kind is worth catching here rather than at the end.
identities=$(security find-identity -v -p codesigning 2>/dev/null || true)

missing_identity() {
    cat >&2 <<MISSING
error: no "$1" certificate found in your keychain.

What you have:
$(echo "$identities" | sed 's/^/    /')

An "Apple Development" certificate is not a substitute — it signs fine and then fails
notarisation. Developer ID certificates need a paid Apple Developer Program membership;
check developer.apple.com/account shows one before going further.

To create them:
    Xcode > Settings > Accounts > your Apple ID > Manage Certificates... > +
        Developer ID Application
        Developer ID Installer

Then re-run: security find-identity -v -p codesigning
MISSING
    exit 1
}

echo "$identities" | grep -q "Developer ID Application" || missing_identity "Developer ID Application"
echo "$identities" | grep -q "Developer ID Installer" || missing_identity "Developer ID Installer"

usage_hint() {
    cat >&2 <<HINT
error: $1 is not set. Use the certificate name exactly as printed, quotes and all:

$(echo "$identities" | grep "Developer ID" | sed 's/^/    /')

    CODESIGN_KEY="Developer ID Application: ..." \\
    PACKAGE_KEY="Developer ID Installer: ..." \\
    $0 ${version:-0.0.0}
HINT
    exit 1
}

[ -n "${CODESIGN_KEY:-}" ] || usage_hint CODESIGN_KEY
[ -n "${PACKAGE_KEY:-}" ] || usage_hint PACKAGE_KEY

# A name that does not match anything in the keychain fails deep inside the build, where the
# message is about a signing step rather than about the name being wrong.
echo "$identities" | grep -qF "$CODESIGN_KEY" \
    || fail "CODESIGN_KEY does not match any identity in your keychain: $CODESIGN_KEY"
echo "$identities" | grep -qF "$PACKAGE_KEY" \
    || fail "PACKAGE_KEY does not match any identity in your keychain: $PACKAGE_KEY"

case "$CODESIGN_KEY" in
    "Developer ID Application:"*) ;;
    *) fail "CODESIGN_KEY must be a 'Developer ID Application' certificate, not: $CODESIGN_KEY" ;;
esac
case "$PACKAGE_KEY" in
    "Developer ID Installer:"*) ;;
    *) fail "PACKAGE_KEY must be a 'Developer ID Installer' certificate, not: $PACKAGE_KEY" ;;
esac

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
