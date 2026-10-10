# Releasing DevGuard

Pushing a version tag builds DevGuard and attaches three packages to one GitHub Release:

- `DevGuard-v1.1.0-win-x64.zip`
- `DevGuard-v1.1.0-osx-arm64.dmg` (Apple Silicon)
- `DevGuard-v1.1.0-osx-x64.dmg` (Intel)

The tag you push is the version. `v1.2.0` produces `DevGuard-v1.2.0-...` assets and a release titled `DevGuard v1.2.0`. The workflow does not create or move tags.

These macOS builds are not signed or notarized. macOS Gatekeeper can warn that the app is from an unidentified developer.

## Create a release

Commit the release workflow first, then tag that commit.

```bash
git tag v1.1.0
git push origin v1.1.0
```

GitHub Actions runs `.github/workflows/release.yml` for tags matching `v*.*.*`.

1. A Windows runner publishes a self-contained `win-x64` app and zips it.
2. A macOS runner publishes `osx-arm64` and `osx-x64`, wraps each in `DevGuard.app`, and creates a DMG.
3. After all three packages exist, one job creates the GitHub Release and uploads them together.

If any build fails, the release is not created.

## Download

Open the repository **Releases** page. The `v1.1.0` release lists the three assets above. GitHub also adds source archives; those are not the app.

## Install

**Windows.** Extract `DevGuard-v1.1.0-win-x64.zip` and run `DevGuard.exe`.

**Mac.** Open the DMG for your Mac. Apple Silicon uses `osx-arm64`. Intel uses `osx-x64`. Drag **DevGuard** onto the **Applications** shortcut, then open DevGuard from Applications.

If macOS says the app cannot be opened because it is from an unidentified developer, the build is unsigned. Open **System Settings → Privacy & Security** and allow DevGuard, or right-click the app, choose **Open**, and confirm. That warning is expected until the app is signed and notarized.

## When a tag already exists

Do not delete or force-push the tag as the first step.

- The workflow failed and no Release was created: open the failed run in the Actions tab and re-run it. Re-running uses the workflow stored in the tagged commit.
- A Release for that tag already exists: the workflow keeps that release and uploads the three packages onto it. A package with the same filename is replaced. You do not need to delete the Release.
- The tag points at a commit that does not contain the fix: ship the fix under a new tag, for example `v1.1.1`, from the updated commit.

```bash
git tag v1.1.1
git push origin v1.1.1
```

Moving `v1.1.0` onto another commit rewrites a published tag. Do that only when you mean to, and only after anyone who already fetched the tag knows it will change.

## Signing and notarization later

The packaging script stops before any `codesign` or `notarytool` command. Add those steps after `DevGuard.app` is built and before the DMG is created. Store the credentials as GitHub Actions secrets. Do not commit certificates or passwords.

Typical secrets:

- `APPLE_CERTIFICATE_P12_BASE64`
- `APPLE_CERTIFICATE_PASSWORD`
- `APPLE_TEAM_ID`
- `APPLE_ID`
- `APPLE_APP_SPECIFIC_PASSWORD`

Sign the `.app`, submit it to Apple notarization, staple the ticket, then build the DMG from that signed app.
