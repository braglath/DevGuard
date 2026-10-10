#!/usr/bin/env bash
# Build a DevGuard.app bundle from a self-contained `dotnet publish` folder
# and package it as a drag-to-Applications DMG.
#
# Native Avalonia libraries are copied next to the executable. This script does
# not sign or notarize the app.

set -euo pipefail

APP_NAME="DevGuard"
BUNDLE_ID="com.devguard.desktop"

usage() {
  cat <<'EOF'
Usage: package-macos-dmg.sh --rid osx-arm64|osx-x64 --tag vX.Y.Z --publish-dir DIR --output FILE
EOF
}

die() {
  echo "error: $*" >&2
  exit 1
}

rid=""
tag=""
publish_dir=""
output=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)
      rid="${2:-}"
      shift 2
      ;;
    --tag)
      tag="${2:-}"
      shift 2
      ;;
    --publish-dir)
      publish_dir="${2:-}"
      shift 2
      ;;
    --output)
      output="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "Unknown argument: $1"
      ;;
  esac
done

[[ -n "$rid" && -n "$tag" && -n "$publish_dir" && -n "$output" ]] || {
  usage
  exit 1
}

case "$rid" in
  osx-arm64)
    expected_arch="arm64"
    ;;
  osx-x64)
    expected_arch="x86_64"
    ;;
  *)
    die "Unsupported runtime identifier: $rid"
    ;;
esac

[[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]] || \
  die "Tag must look like v1.1.0. Got: $tag"

[[ -d "$publish_dir" ]] || die "Publish directory does not exist: $publish_dir"

publish_dir="$(cd "$publish_dir" && pwd)"
output_dir="$(dirname "$output")"
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
output="$output_dir/$(basename "$output")"

executable="$publish_dir/$APP_NAME"
[[ -f "$executable" ]] || die "Publish did not produce $executable"
[[ -s "$executable" ]] || die "Published executable is empty: $executable"

if ! find "$publish_dir" -name 'libAvaloniaNative.dylib' -print -quit | grep -q .; then
  die "Avalonia native library libAvaloniaNative.dylib is missing from $publish_dir"
fi

short_version="${tag#v}"
bundle_version="${short_version%%-*}"
bundle_version="${bundle_version%%+*}"

work_dir="$(mktemp -d)"
mount_point=""

cleanup() {
  if [[ -n "$mount_point" ]]; then
    hdiutil detach "$mount_point" >/dev/null 2>&1 || true
    mount_point=""
  fi
  rm -rf "$work_dir"
}
trap cleanup EXIT

app_bundle="$work_dir/$APP_NAME.app"
macos_dir="$app_bundle/Contents/MacOS"
mkdir -p "$macos_dir"

while IFS= read -r -d '' file; do
  relative="${file#"$publish_dir"/}"
  destination="$macos_dir/$relative"
  mkdir -p "$(dirname "$destination")"
  cp -p "$file" "$destination"
done < <(find "$publish_dir" -type f ! -name '*.pdb' ! -name '*.xml' -print0)

chmod +x "$macos_dir/$APP_NAME"
find "$macos_dir" -name '*.dylib' -exec chmod +x {} +

plist="$app_bundle/Contents/Info.plist"
cat > "$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_ID</string>
  <key>CFBundleExecutable</key>
  <string>$APP_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleShortVersionString</key>
  <string>$bundle_version</string>
  <key>CFBundleVersion</key>
  <string>$bundle_version</string>
  <key>CFBundleSupportedPlatforms</key>
  <array>
    <string>MacOSX</string>
  </array>
  <key>LSArchitecturePriority</key>
  <array>
    <string>$expected_arch</string>
  </array>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSPrincipalClass</key>
  <string>NSApplication</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.developer-tools</string>
</dict>
</plist>
EOF

printf 'APPL????' > "$app_bundle/Contents/PkgInfo"

plutil -lint "$plist" >/dev/null
executable_name="$(plutil -extract CFBundleExecutable raw "$plist")"
[[ "$executable_name" == "$APP_NAME" ]] || die "Info.plist executable is '$executable_name'"
[[ -x "$macos_dir/$APP_NAME" ]] || die "App bundle executable is missing or not executable"
[[ -s "$macos_dir/$APP_NAME" ]] || die "App bundle executable is empty"
[[ -f "$plist" ]] || die "Info.plist was not written"

actual_arch="$(lipo -archs "$macos_dir/$APP_NAME")"
[[ "$actual_arch" == "$expected_arch" ]] || \
  die "$rid executable architecture is '$actual_arch', expected '$expected_arch'"

file "$macos_dir/$APP_NAME" | grep -q "Mach-O" || \
  die "App executable is not a Mach-O binary"

if ! find "$macos_dir" -name 'libAvaloniaNative.dylib' -print -quit | grep -q .; then
  die "App bundle is missing libAvaloniaNative.dylib"
fi

# Signing and notarization are intentionally skipped. Add them here, before the
# disk image is created, once Apple credentials are available as CI secrets.

app_kb="$(du -sk "$app_bundle" | awk '{print $1}')"
size_kb="$((app_kb + app_kb / 5 + 32768))"
scratch_dmg="$work_dir/scratch.dmg"
mount_point="$work_dir/mnt"
mkdir -p "$mount_point"

hdiutil create \
  -size "${size_kb}k" \
  -fs HFS+ \
  -volname "$APP_NAME" \
  "$scratch_dmg"

hdiutil attach "$scratch_dmg" \
  -mountpoint "$mount_point" \
  -nobrowse \
  -noverify

cp -R "$app_bundle" "$mount_point/$APP_NAME.app"
ln -s /Applications "$mount_point/Applications"

[[ -x "$mount_point/$APP_NAME.app/Contents/MacOS/$APP_NAME" ]] || \
  die "Mounted disk image is missing the app executable"
[[ -f "$mount_point/$APP_NAME.app/Contents/Info.plist" ]] || \
  die "Mounted disk image is missing Info.plist"
[[ -L "$mount_point/Applications" ]] || \
  die "Mounted disk image is missing the Applications shortcut"

hdiutil detach "$mount_point"
mount_point=""

rm -f "$output"
hdiutil convert "$scratch_dmg" \
  -format UDZO \
  -imagekey zlib-level=9 \
  -o "$output"

[[ -s "$output" ]] || die "DMG is missing or empty: $output"
hdiutil verify "$output" >/dev/null

echo "Created $output"
