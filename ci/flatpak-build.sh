#!/usr/bin/env bash
set -euo pipefail

MANIFEST=""
PUBLISHED_DIR="artifacts/published"
OUT_DIR="artifacts"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --manifest) MANIFEST="$2"; shift 2;;
    --published-dir) PUBLISHED_DIR="$2"; shift 2;;
    --out) OUT_DIR="$2"; shift 2;;
    *) shift;;
  esac
done

if [[ -z "$MANIFEST" ]]; then
  echo "Manifest not provided. Use --manifest path/to/manifest.json"
  exit 1
fi

echo "Using manifest: $MANIFEST"
echo "Published dir: $PUBLISHED_DIR"

BUILD_DIR="${FLATPAK_BUILD_DIR:-build-dir}"
REPO_DIR="${FLATPAK_REPO_DIR:-repo}"
BUNDLE_NAME="${FLATPAK_BUNDLE_NAME:-org.filekeeper.FileKeeper.flatpak}"

rm -rf "$BUILD_DIR" "$REPO_DIR"
mkdir -p "$BUILD_DIR" "$REPO_DIR"

# Copy published app into a temp dir that the manifest references
# Replace ${PUBLISHED_DIR} variable in manifest with the actual path by creating a temp manifest
TEMP_MANIFEST="${BUILD_DIR}/manifest.json"
mkdir -p "${BUILD_DIR}"

# Expand PUBLISHED_DIR to absolute path to allow flatpak-builder copying
PUBLISHED_ABS=$(realpath "$PUBLISHED_DIR")

echo "Generating temporary manifest at $TEMP_MANIFEST"
cat "$MANIFEST" | sed "s|${PUBLISHED_DIR}|${PUBLISHED_ABS}|g" > "$TEMP_MANIFEST"

# Run flatpak-builder
flatpak-builder --force-clean --repo="$REPO_DIR" "$BUILD_DIR" "$TEMP_MANIFEST"

# Create a bundle
mkdir -p "$OUT_DIR"
flatpak build-bundle "$REPO_DIR" "$OUT_DIR/$BUNDLE_NAME" "org.freedesktop.Platform/x86_64/23.08"

echo "Bundle created at $OUT_DIR/$BUNDLE_NAME"

