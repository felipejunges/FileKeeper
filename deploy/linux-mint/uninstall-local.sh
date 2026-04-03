#!/usr/bin/env bash
set -euo pipefail

DISABLE_SERVICE="yes"
if [[ $# -gt 1 ]]; then
  echo "Usage: $0 [--disable-service|--keep-service]"
  exit 1
fi

if [[ $# -eq 1 ]]; then
  case "$1" in
    --disable-service)
      DISABLE_SERVICE="yes"
      ;;
    --keep-service)
      DISABLE_SERVICE="no"
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--disable-service|--keep-service]"
      exit 1
      ;;
  esac
fi

APP_DIR="$HOME/.local/opt/filekeeper/publish"
SERVICE_FILE="$HOME/.config/systemd/user/filekeeper.service"
DESKTOP_FILE="$HOME/.local/share/applications/filekeeper.desktop"

if [[ "$DISABLE_SERVICE" == "yes" ]]; then
  systemctl --user disable --now filekeeper.service >/dev/null 2>&1 || true
fi

rm -f "$SERVICE_FILE"
rm -f "$DESKTOP_FILE"
rm -rf "$APP_DIR"

systemctl --user daemon-reload >/dev/null 2>&1 || true

echo "FileKeeper removed from: $APP_DIR"
if [[ "$DISABLE_SERVICE" == "yes" ]]; then
  echo "Systemd user service: filekeeper.service (stopped/disabled and removed)"
else
  echo "Systemd user service file removed (service stop/disable skipped)"
fi
echo "Desktop launcher removed: $DESKTOP_FILE"