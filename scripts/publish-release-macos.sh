#!/usr/bin/env bash
set -euo pipefail

version="1.0.0-preview.1"
runtime="${1:-osx-arm64}"
case "$runtime" in
  osx-arm64|osx-x64) ;;
  *) printf 'Unsupported macOS runtime: %s\n' "$runtime" >&2; exit 2 ;;
esac

if [[ "$(uname -s)" != "Darwin" ]]; then
  printf 'This release script must run on macOS.\n' >&2
  exit 2
fi

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/src/RepoGalaxy.Desktop/RepoGalaxy.Desktop.csproj"
release_root="$repo_root/release"
final_directory="$release_root/${version}-${runtime}"
staging_parent="$repo_root/obj/release-staging"
mkdir -p "$staging_parent"
staging_root="$(mktemp -d "$staging_parent/macos.XXXXXX")"
publish_directory="$staging_root/publish"
package_directory="$staging_root/package"
dmg_root="$staging_root/dmg"
app_bundle="$package_directory/RepoGalaxy.app"
iconset="$staging_root/repogalaxy.iconset"
backup_directory=""

assert_repository_path() {
  case "$1" in
    "$repo_root"/*) ;;
    *) printf 'Refusing to modify a path outside the repository: %s\n' "$1" >&2; exit 2 ;;
  esac
}

cleanup() {
  rm -rf "$staging_root"
}
trap cleanup EXIT

assert_repository_path "$staging_root"
assert_repository_path "$final_directory"
mkdir -p "$publish_directory" "$package_directory" "$dmg_root" "$iconset"

dotnet_host="$(command -v dotnet || true)"
if [[ -x "$HOME/.dotnet/dotnet" ]] && "$HOME/.dotnet/dotnet" --version >/dev/null 2>&1; then
  dotnet_host="$HOME/.dotnet/dotnet"
fi
if [[ -z "$dotnet_host" ]]; then
  printf '.NET 10 SDK is required to publish the macOS release.\n' >&2
  exit 1
fi

"$dotnet_host" publish "$project" \
  --configuration Release \
  --runtime "$runtime" \
  --self-contained true \
  --output "$publish_directory" \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugType=None \
  -p:DebugSymbols=false

published_executable="$publish_directory/RepoGalaxy.Desktop"
if [[ ! -f "$published_executable" ]]; then
  printf 'The expected macOS executable was not produced.\n' >&2
  exit 1
fi

mkdir -p "$app_bundle/Contents/MacOS" "$app_bundle/Contents/Resources"
cp "$published_executable" "$app_bundle/Contents/MacOS/RepoGalaxy"
chmod 755 "$app_bundle/Contents/MacOS/RepoGalaxy"
cp "$repo_root/packaging/macos/Info.plist" "$app_bundle/Contents/Info.plist"

icon_source="$repo_root/src/RepoGalaxy.Desktop/Assets/repogalaxy-logo.png"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$icon_source" --out "$iconset/icon_${size}x${size}.png" >/dev/null
done
for size in 32 64 256 512 1024; do
  base_size=$((size / 2))
  sips -z "$size" "$size" "$icon_source" --out "$iconset/icon_${base_size}x${base_size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app_bundle/Contents/Resources/repogalaxy.icns"

codesign --force --deep --sign - --timestamp=none "$app_bundle"
codesign --verify --deep --strict "$app_bundle"

ditto "$app_bundle" "$dmg_root/RepoGalaxy.app"
ln -s /Applications "$dmg_root/Applications"
dmg_path="$package_directory/RepoGalaxy-$version-$runtime.dmg"
hdiutil create -volname RepoGalaxy -srcfolder "$dmg_root" -ov -format UDZO "$dmg_path" >/dev/null
cp "$repo_root/packaging/macos/README.md" "$package_directory/README.md"

(
  cd "$package_directory"
  shasum -a 256 "RepoGalaxy-$version-$runtime.dmg" > SHA256SUMS.txt
)

mkdir -p "$(dirname "$final_directory")"
if [[ -e "$final_directory" ]]; then
  backup_directory="$release_root/.$(basename "$final_directory").previous-$(uuidgen | tr '[:upper:]' '[:lower:]')"
  assert_repository_path "$backup_directory"
  mv "$final_directory" "$backup_directory"
fi

if ! mv "$package_directory" "$final_directory"; then
  if [[ -n "$backup_directory" && -e "$backup_directory" && ! -e "$final_directory" ]]; then
    mv "$backup_directory" "$final_directory"
    backup_directory=""
  fi
  exit 1
fi

if [[ -n "$backup_directory" ]]; then
  assert_repository_path "$backup_directory"
  rm -rf "$backup_directory"
fi

printf 'Published RepoGalaxy %s (%s) to:\n  %s\n' "$version" "$runtime" "$final_directory"
