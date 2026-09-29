#!/usr/bin/env bash
set -euo pipefail

version="1.0.0-preview.1"
runtime="linux-x64"

if [[ "$(uname -s)" != "Linux" ]]; then
  printf 'This release script must run on Linux.\n' >&2
  exit 2
fi

case "$(uname -m)" in
  x86_64|amd64) ;;
  *)
    printf 'This release script requires an x86_64 Linux host.\n' >&2
    exit 2
    ;;
esac

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/src/RepoGalaxy.Desktop/RepoGalaxy.Desktop.csproj"
release_root="$repo_root/release"
final_directory="$release_root/${version}-${runtime}"
staging_parent="$repo_root/obj/release-staging"
mkdir -p "$staging_parent"
staging_root="$(mktemp -d "$staging_parent/linux.XXXXXX")"
publish_directory="$staging_root/publish"
app_dir="$staging_root/RepoGalaxy.AppDir"
package_directory="$staging_root/package"
appimage_name="RepoGalaxy-$version-$runtime.AppImage"
appimage_path="$package_directory/$appimage_name"
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

if ! command -v appimagetool >/dev/null 2>&1; then
  printf 'appimagetool is required to build the Linux AppImage.\n' >&2
  printf 'Install appimagetool and run this script on Ubuntu 22.04 x86_64 or a compatible Linux host.\n' >&2
  exit 1
fi

dotnet_host="$(command -v dotnet || true)"
if [[ -x "$HOME/.dotnet/dotnet" ]] && "$HOME/.dotnet/dotnet" --version >/dev/null 2>&1; then
  dotnet_host="$HOME/.dotnet/dotnet"
fi
if [[ -z "$dotnet_host" ]]; then
  printf '.NET 10 SDK is required to publish the Linux release.\n' >&2
  exit 1
fi

mkdir -p "$publish_directory" "$app_dir/usr/bin" "$package_directory"

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
  printf 'The expected Linux executable was not produced.\n' >&2
  exit 1
fi

install -m 755 "$published_executable" "$app_dir/usr/bin/RepoGalaxy"
install -m 755 "$repo_root/packaging/linux/AppRun" "$app_dir/AppRun"
install -m 644 "$repo_root/packaging/linux/repogalaxy.desktop" "$app_dir/RepoGalaxy.desktop"
install -m 644 "$repo_root/src/RepoGalaxy.Desktop/Assets/repogalaxy-logo.png" "$app_dir/repogalaxy.png"
ln -s repogalaxy.png "$app_dir/.DirIcon"

(
  cd "$package_directory"
  ARCH=x86_64 appimagetool "$app_dir" "$appimage_path"
)

cp "$repo_root/packaging/linux/README.md" "$package_directory/README.md"
(
  cd "$package_directory"
  sha256sum "$appimage_name" > SHA256SUMS.txt
)

mkdir -p "$release_root"
if [[ -e "$final_directory" ]]; then
  backup_directory="$release_root/.$(basename "$final_directory").previous-$(date +%s)-$$"
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
