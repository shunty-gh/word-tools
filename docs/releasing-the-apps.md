# Releasing the apps

The CLI releases itself: push a `v*` tag and the workflow builds six binaries and the
`dotnet tool` package. The apps need signing material that cannot live in the repository,
so each platform has a one-time setup before a tag produces anything.

Neither route below puts the app in a store. That is deliberate for now — a downloadable
APK and a notarised `.pkg` reach people without a review queue, a Play Console account, or
a listing to maintain, and both are the fastest way to get the app onto a physical device.
The stores remain open later; nothing here forecloses them.

## Android — signed APK, attached to the release

Once set up, this is automatic: a `v*` tag builds `words-<version>-android.apk` and attaches
it to the draft release alongside the CLI binaries.

### 1. Create a keystore

**Keep this file. Losing it is not recoverable.** Android identifies an app by its signing
key, so a replacement key cannot upgrade an installation signed with the old one — every
user has to uninstall and reinstall, losing their personal word list. If the app ever goes
to Google Play, a lost key is worse still.

Back it up somewhere durable — a password manager holds the file and both passwords well.
It is not in this repository and never should be.

```bash
keytool -genkeypair -v \
  -keystore words.keystore \
  -alias words \
  -keyalg RSA -keysize 4096 \
  -validity 10000 \
  -storetype pkcs12
```

It asks for a password and for your name and organisation. The name shows up only in the
certificate, not to users. Use the same value for the store and key password unless you have
a reason not to — `-storetype pkcs12` keeps them in step anyway.

### 2. Add four repository secrets

Settings → Secrets and variables → Actions → New repository secret.

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | `base64 -i words.keystore \| pbcopy` |
| `ANDROID_KEYSTORE_PASSWORD` | the store password |
| `ANDROID_KEY_PASSWORD` | the key password |
| `ANDROID_KEY_ALIAS` | `words`, or whatever `-alias` you used |

The base64 step is only because a secret holds text, not a file; the workflow decodes it back
into a keystore on the runner and never prints it.

### 3. Tag

That is all. The `android` job is skipped whenever `ANDROID_KEYSTORE_BASE64` is absent, so
the release still works before this is done — it just has no APK in it. Once the secret
exists, a failed APK build blocks the release rather than shipping one silently missing the
app.

### Installing the result

An APK from outside the Play Store needs "install unknown apps" allowed for whichever app
is doing the installing (usually the browser or the file manager). Android says so clearly
at the moment it matters, so the download page needs no instructions.

## Mac Catalyst — notarised, outside the Mac App Store

This one is a local script, [`scripts/package-maccatalyst.sh`](../../scripts/package-maccatalyst.sh),
not a CI job. Notarisation fails in opaque ways and each attempt through CI costs minutes;
on your own Mac the loop is seconds and the error is in front of you. Once it runs clean,
moving it into `release.yml` is mechanical — the commands do not change, only where the
certificates come from.

### What it needs

1. **An Apple Developer Program membership** — you have this.

2. **Two certificates** in your login keychain, from
   [developer.apple.com](https://developer.apple.com/account/resources/certificates):
   *Developer ID Application* signs the `.app`, *Developer ID Installer* signs the `.pkg`.
   `security find-identity -v` lists what you already have, in exactly the form the script
   wants.

3. **A stored notarytool credential**, so nothing handles your password at run time:

   ```bash
   xcrun notarytool store-credentials words-notary \
     --apple-id you@example.com \
     --team-id YOURTEAMID \
     --password <app-specific password from appleid.apple.com>
   ```

   The app-specific password is generated at appleid.apple.com, not your Apple ID password.

### Running it

```bash
CODESIGN_KEY="Developer ID Application: Your Name (TEAMID)" \
PACKAGE_KEY="Developer ID Installer: Your Name (TEAMID)" \
scripts/package-maccatalyst.sh 0.1.0
```

It publishes for both architectures — a Developer ID build is downloaded by strangers on
hardware you cannot predict, so an arm64-only package would just fail to launch on an Intel
Mac — then signs, notarises, staples and verifies. Attach the result to the release:

```bash
gh release upload v0.1.0 publish/maccatalyst/words-0.1.0-maccatalyst.pkg
```

### When it goes wrong

Notarisation rejections come back as a submission id. The log says what Apple objected to:

```bash
xcrun notarytool log <submission-id> --keychain-profile words-notary
```

The usual causes are a missing hardened runtime (the script sets `UseHardenedRuntime`), an
unsigned nested binary, or a certificate that is not a *Developer ID* one — a Mac App Store
or Development certificate will sign happily and then fail notarisation.

## iOS

Not set up. It needs the same Apple membership plus a provisioning profile, and without the
App Store or TestFlight there is no route to anyone else's device worth the effort — ad-hoc
distribution requires collecting device UDIDs in advance. Worth doing when the app is going
to the App Store, and not much before.

## What is still unmeasured

`docs/plan-maui.md` records that the app's memory has only ever been measured on a simulator,
which runs on the Mac's RAM with no jetsam limits. The Android APK is the cheapest way to
settle that: install it on a real phone and watch. If it is a problem, the fix is the entry
flattening described in that plan — roughly 160 MB down to 60 MB.
