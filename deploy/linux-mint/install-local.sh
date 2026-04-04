#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish"

ENABLE_SERVICE="ask"
DO_PUBLISH="yes"
PUBLISH_CONFIGURATION="Release"
PUBLISH_RUNTIME="linux-x64"

print_help() {
  cat <<EOF
Usage: $0 [options]

Options:
  --enable-service           Enable and start user service after install
  --no-service               Do not enable service (default asks interactively)
  --publish                  Run dotnet publish before install (default)
  --no-publish               Skip publish and use existing publish folder
  --configuration=<VALUE>    Publish configuration (default: Release)
  --runtime=<RID>            Publish runtime RID (default: linux-x64)
  -h, --help                 Show this help message

Examples:
  $0
  $0 --no-service
  $0 --configuration=Debug --runtime=linux-x64
  $0 --no-publish --enable-service
EOF
}

for arg in "$@"; do
  case "$arg" in
    -h|--help)
      print_help
      exit 0
      ;;
    --enable-service)
      ENABLE_SERVICE="yes"
      ;;
    --no-service)
      ENABLE_SERVICE="no"
      ;;
    --no-publish)
      DO_PUBLISH="no"
      ;;
    --publish)
      DO_PUBLISH="yes"
      ;;
    --configuration=*)
      PUBLISH_CONFIGURATION="${arg#*=}"
      ;;
    --runtime=*)
      PUBLISH_RUNTIME="${arg#*=}"
      ;;
    *)
      echo "Unknown option: $arg"
      echo
      print_help
      exit 1
      ;;
  esac
done

APP_DIR="$HOME/.local/opt/filekeeper/publish"
SYSTEMD_USER_DIR="$HOME/.config/systemd/user"
APPLICATIONS_DIR="$HOME/.local/share/applications"
UI_PROJECT="$PROJECT_ROOT/FileKeeper.UI/FileKeeper.UI.csproj"
ICON_SOURCE="$PROJECT_ROOT/FileKeeper.UI/Assets/appicon/filekeeper_edited.ico"
ICON_TARGET="$APP_DIR/filekeeper_edited.ico"

if [[ "$DO_PUBLISH" == "yes" ]]; then
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet SDK is required to publish. Install it or run with --no-publish."
    exit 1
  fi

  echo "Publishing FileKeeper.UI ($PUBLISH_CONFIGURATION, $PUBLISH_RUNTIME)..."
  dotnet publish "$UI_PROJECT" -c "$PUBLISH_CONFIGURATION" -r "$PUBLISH_RUNTIME" --self-contained true -o "$PUBLISH_DIR"
fi

if [[ ! -f "$PUBLISH_DIR/FileKeeper.UI" ]]; then
  echo "Missing publish output at: $PUBLISH_DIR"
  echo "Run this installer without --no-publish, or publish manually first."
  exit 1
fi

mkdir -p "$APP_DIR" "$SYSTEMD_USER_DIR" "$APPLICATIONS_DIR"

cp -a "$PUBLISH_DIR/." "$APP_DIR/"
chmod +x "$APP_DIR/FileKeeper.UI"
find "$APP_DIR" -maxdepth 1 -type f -name '*.so' -exec chmod +x {} \;

if [[ -f "$ICON_SOURCE" ]]; then
  cp -f "$ICON_SOURCE" "$ICON_TARGET"
fi

install -m 0644 "$SCRIPT_DIR/filekeeper-user.service" "$SYSTEMD_USER_DIR/filekeeper.service"

cat > "$APPLICATIONS_DIR/filekeeper.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=FileKeeper
Comment=Backup and restore manager
Exec=env LD_LIBRARY_PATH=$APP_DIR $APP_DIR/FileKeeper.UI
Path=$APP_DIR
Icon=$ICON_TARGET
Terminal=false
Categories=Utility;
StartupNotify=true
EOF
chmod 0644 "$APPLICATIONS_DIR/filekeeper.desktop"

if [[ "$ENABLE_SERVICE" == "ask" ]]; then
  read -r -p "Enable auto-start service (systemd --user)? [y/N]: " REPLY
  if [[ "$REPLY" =~ ^[Yy]$ ]]; then
    ENABLE_SERVICE="yes"
  else
    ENABLE_SERVICE="no"
  fi
fi

if [[ "$ENABLE_SERVICE" == "yes" ]]; then
  systemctl --user daemon-reload
  systemctl --user enable --now filekeeper.service
  echo "Systemd user service: filekeeper.service (enabled and started)"
else
  echo "Systemd user service: filekeeper.service (installed, not enabled)"
fi

echo "FileKeeper installed to: $APP_DIR"
echo "Desktop launcher: $APPLICATIONS_DIR/filekeeper.desktop"

rm -rf "$PUBLISH_DIR"
echo "Cleaned up publish folder: $PUBLISH_DIR"