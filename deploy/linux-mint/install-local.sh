#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish"

ENABLE_SERVICE="ask"
if [[ $# -gt 1 ]]; then
  echo "Usage: $0 [--enable-service|--no-service]"
  exit 1
fi

if [[ $# -eq 1 ]]; then
  case "$1" in
    --enable-service)
      ENABLE_SERVICE="yes"
      ;;
    --no-service)
      ENABLE_SERVICE="no"
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--enable-service|--no-service]"
      exit 1
      ;;
  esac
fi

APP_DIR="$HOME/.local/opt/filekeeper/publish"
SYSTEMD_USER_DIR="$HOME/.config/systemd/user"
APPLICATIONS_DIR="$HOME/.local/share/applications"

if [[ ! -f "$PUBLISH_DIR/FileKeeper.UI" ]]; then
  echo "Missing publish output at: $PUBLISH_DIR"
  echo "Build and publish first, then run this installer again."
  exit 1
fi

mkdir -p "$APP_DIR" "$SYSTEMD_USER_DIR" "$APPLICATIONS_DIR"

cp -a "$PUBLISH_DIR/." "$APP_DIR/"
chmod +x "$APP_DIR/FileKeeper.UI"
find "$APP_DIR" -maxdepth 1 -type f -name '*.so' -exec chmod +x {} \;

install -m 0644 "$SCRIPT_DIR/filekeeper-user.service" "$SYSTEMD_USER_DIR/filekeeper.service"

cat > "$APPLICATIONS_DIR/filekeeper.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=FileKeeper
Comment=Backup and restore manager
Exec=env LD_LIBRARY_PATH=$APP_DIR $APP_DIR/FileKeeper.UI
Path=$APP_DIR
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