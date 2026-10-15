#!/usr/bin/env bash
# Builds the FileKeeper installers into artifacts/:
#   FileKeeper-<version>-linux-amd64.deb     (Debian, Ubuntu, Mint, ...)
#   FileKeeper-<version>-win-x64-setup.exe   (Windows 10/11, x64)
# Both bundle the .NET runtime, so nothing else needs to be installed.
#
# Requirements: .NET SDK, dpkg-deb (Linux package), Docker (Windows installer, runs Inno Setup).
# Usage: packaging/build-installers.sh [linux|windows]...   (default: both)
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo/FileKeeper.App/FileKeeper.App.csproj"
artifacts="$repo/artifacts"
version="$(dotnet msbuild "$project" -getProperty:Version)"
targets=("${@:-linux windows}")
targets=(${targets[*]})

publish() {
    local rid="$1" out="$artifacts/publish/$1"
    echo "==> Publishing $rid"
    rm -rf "$out"
    dotnet publish "$project" -c Release -r "$rid" --self-contained -o "$out" \
        -p:DebugType=None -p:DebugSymbols=false
}

build_linux() {
    publish linux-x64
    echo "==> Building .deb"
    local root="$artifacts/deb/filekeeper"
    rm -rf "$root"
    mkdir -p "$root/DEBIAN" "$root/opt" "$root/usr/bin" "$root/usr/share/applications"

    cp -r "$artifacts/publish/linux-x64" "$root/opt/filekeeper"
    ln -s /opt/filekeeper/FileKeeper.App "$root/usr/bin/filekeeper"
    install -m 644 "$repo/packaging/linux/filekeeper.desktop" "$root/usr/share/applications/"
    for icon in "$repo"/packaging/linux/icons/*.png; do
        local size; size="$(basename "$icon" .png)"
        install -D -m 644 "$icon" "$root/usr/share/icons/hicolor/${size}x${size}/apps/filekeeper.png"
    done

    cat > "$root/DEBIAN/control" <<CONTROL
Package: filekeeper
Version: $version
Architecture: amd64
Maintainer: Felipe <felipejunges@yahoo.com.br>
Section: utils
Priority: optional
Installed-Size: $(du -sk "$root" | cut -f1)
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libfontconfig1, libx11-6, libice6, libsm6, libicu76 | libicu74 | libicu72 | libicu70
Description: Incremental folder backups into a single zip file
 FileKeeper keeps snapshots of your folders in one zip archive, storing only
 new or modified files, and lets you browse and restore any version.
CONTROL
    # Refresh the menu icon cache when available.
    cat > "$root/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q /usr/share/icons/hicolor || true
POSTINST
    chmod 755 "$root/DEBIAN/postinst"
    # Installed files must not be group/world writable, whatever the local umask.
    chmod -R u+rwX,go+rX,go-w "$root"

    dpkg-deb --root-owner-group --build "$root" "$artifacts/FileKeeper-$version-linux-amd64.deb"
}

build_windows() {
    publish win-x64
    echo "==> Building Windows installer (Inno Setup in Docker)"
    docker run --rm -v "$repo:/work" -w /work/packaging/windows amake/innosetup \
        "/DAppVersion=$version" FileKeeper.iss
}

mkdir -p "$artifacts"
for target in "${targets[@]}"; do
    case "$target" in
        linux) build_linux ;;
        windows) build_windows ;;
        *) echo "Unknown target: $target (expected linux or windows)" >&2; exit 1 ;;
    esac
done

echo "==> Done:"
ls -lh "$artifacts"/*.deb "$artifacts"/*.exe 2>/dev/null || true
