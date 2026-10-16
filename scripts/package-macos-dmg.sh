#!/usr/bin/env bash
# Build a "Super Dev.app" bundle from a self-contained `dotnet publish` folder
# and package it as a drag-to-Applications DMG.
#
# Native Avalonia libraries are copied next to the executable. Every file in the
# bundle is ad-hoc signed, managed DLLs included, so Apple Silicon can launch it.
# A downloaded copy still needs Apple notarization before Gatekeeper will open it.

set -euo pipefail

APP_EXECUTABLE="Super Dev"
APP_DISPLAY_NAME="Super Dev"
BUNDLE_ID="com.superdev.desktop"

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

executable="$publish_dir/$APP_EXECUTABLE"
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

app_bundle="$work_dir/$APP_DISPLAY_NAME.app"
macos_dir="$app_bundle/Contents/MacOS"
mkdir -p "$macos_dir"

while IFS= read -r -d '' file; do
  relative="${file#"$publish_dir"/}"
  destination="$macos_dir/$relative"
  mkdir -p "$(dirname "$destination")"
  cp -p "$file" "$destination"
done < <(find "$publish_dir" -type f ! -name '*.pdb' ! -name '*.xml' -print0)

chmod +x "$macos_dir/$APP_EXECUTABLE"
find "$macos_dir" -name '*.dylib' -exec chmod +x {} +

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
icon_path="$repo_root/Assets/AppIcon.icns"
[[ -s "$icon_path" ]] || die "App icon is missing: $icon_path"
mkdir -p "$app_bundle/Contents/Resources"
cp "$icon_path" "$app_bundle/Contents/Resources/AppIcon.icns"

plist="$app_bundle/Contents/Info.plist"
cat > "$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$APP_DISPLAY_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_DISPLAY_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_ID</string>
  <key>CFBundleExecutable</key>
  <string>$APP_EXECUTABLE</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
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
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key>
      <string>Super Dev Checklist</string>
      <key>CFBundleTypeRole</key>
      <string>Editor</string>
      <key>LSHandlerRank</key>
      <string>Owner</string>
      <key>LSItemContentTypes</key>
      <array>
        <string>com.superdev.checklist</string>
      </array>
    </dict>
  </array>
  <key>UTExportedTypeDeclarations</key>
  <array>
    <dict>
      <key>UTTypeIdentifier</key>
      <string>com.superdev.checklist</string>
      <key>UTTypeDescription</key>
      <string>Super Dev Checklist</string>
      <key>UTTypeConformsTo</key>
      <array>
        <string>public.json</string>
        <string>public.data</string>
      </array>
      <key>UTTypeTagSpecification</key>
      <dict>
        <key>public.filename-extension</key>
        <array>
          <string>superdev.json</string>
        </array>
      </dict>
    </dict>
  </array>
</dict>
</plist>
EOF

printf 'APPL????' > "$app_bundle/Contents/PkgInfo"

plutil -lint "$plist" >/dev/null
executable_name="$(plutil -extract CFBundleExecutable raw "$plist")"
[[ "$executable_name" == "$APP_EXECUTABLE" ]] || die "Info.plist executable is '$executable_name'"
[[ -x "$macos_dir/$APP_EXECUTABLE" ]] || die "App bundle executable is missing or not executable"
[[ -s "$macos_dir/$APP_EXECUTABLE" ]] || die "App bundle executable is empty"
[[ -f "$plist" ]] || die "Info.plist was not written"
[[ -s "$app_bundle/Contents/Resources/AppIcon.icns" ]] || die "App icon was not added to the bundle"

actual_arch="$(lipo -archs "$macos_dir/$APP_EXECUTABLE")"
[[ "$actual_arch" == "$expected_arch" ]] || \
  die "$rid executable architecture is '$actual_arch', expected '$expected_arch'"

file "$macos_dir/$APP_EXECUTABLE" | grep -q "Mach-O" || \
  die "App executable is not a Mach-O binary"

if ! find "$macos_dir" -name 'libAvaloniaNative.dylib' -print -quit | grep -q .; then
  die "App bundle is missing libAvaloniaNative.dylib"
fi

[[ -s "$macos_dir/libSuperDevNotify.dylib" ]] || \
  die "App bundle is missing libSuperDevNotify.dylib"

while IFS= read -r -d '' candidate; do
  if ! archs="$(lipo -archs "$candidate" 2>/dev/null)"; then
    continue
  fi
  if [[ " $archs " != *" $expected_arch "* ]]; then
    die "$candidate is '$archs', expected '$expected_arch'"
  fi
done < <(find "$macos_dir" -type f -print0)

# Apple Silicon rejects an unsigned app bundle and reports it as damaged.
# codesign treats the managed DLLs next to the executable as nested code, so
# those files must be signed before the executable. The bundle is signed last.
# Ad-hoc identity "-" is valid for the kernel; Gatekeeper still blocks a
# downloaded copy until it is notarized.
xattr -cr "$app_bundle"
while IFS= read -r -d '' candidate; do
  if [[ "$candidate" == "$macos_dir/$APP_EXECUTABLE" ]]; then
    continue
  fi
  codesign --force --sign - "$candidate"
done < <(find "$app_bundle" -type f -print0)
codesign --force --sign - "$macos_dir/$APP_EXECUTABLE"
codesign --force --sign - "$app_bundle"
codesign --verify --strict --verbose=2 "$app_bundle"

app_kb="$(du -sk "$app_bundle" | awk '{print $1}')"
size_kb="$((app_kb + app_kb / 5 + 32768))"
scratch_dmg="$work_dir/scratch.dmg"
mount_point="$work_dir/mnt"
mkdir -p "$mount_point"

hdiutil create \
  -size "${size_kb}k" \
  -fs HFS+ \
  -volname "$APP_DISPLAY_NAME" \
  "$scratch_dmg"

hdiutil attach "$scratch_dmg" \
  -mountpoint "$mount_point" \
  -nobrowse \
  -noverify

cp -R "$app_bundle" "$mount_point/$APP_DISPLAY_NAME.app"
ln -s /Applications "$mount_point/Applications"

[[ -x "$mount_point/$APP_DISPLAY_NAME.app/Contents/MacOS/$APP_EXECUTABLE" ]] || \
  die "Mounted disk image is missing the app executable"
codesign --verify --strict --verbose=2 "$mount_point/$APP_DISPLAY_NAME.app"
[[ -f "$mount_point/$APP_DISPLAY_NAME.app/Contents/Info.plist" ]] || \
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
