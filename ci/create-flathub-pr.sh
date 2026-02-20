#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${GITHUB_TOKEN:-}" || -z "${FLATHUB_APP_ID:-}" || -z "${GITHUB_FORK_OWNER:-}" ]]; then
  echo "GITHUB_TOKEN, FLATHUB_APP_ID and GITHUB_FORK_OWNER must be set to create a PR. Skipping."
  exit 0
fi

# Minimal script: fork flathub/apps, create branch, copy manifest and open PR
UPSTREAM_OWNER="flathub"
UPSTREAM_REPO="apps"
FORK_OWNER="$GITHUB_FORK_OWNER"
BRANCH_NAME="ci/update-manifest-$(date +%Y%m%d%H%M%S)"

TMPDIR=$(mktemp -d)
cd "$TMPDIR"

# clone the fork if exists, otherwise fork via API
if ! git ls-remote "https://github.com/${FORK_OWNER}/${UPSTREAM_REPO}.git" &>/dev/null; then
  echo "Fork does not exist or is not reachable: https://github.com/${FORK_OWNER}/${UPSTREAM_REPO}.git"
  echo "Ensure your user has a fork of flathub/apps. Aborting."
  exit 1
fi

# clone fork
git clone "https://github.com/${FORK_OWNER}/${UPSTREAM_REPO}.git" repo
cd repo

git checkout -b "$BRANCH_NAME"

# Copy our manifest into apps/<FLATHUB_APP_ID>/current/manifest.json
DEST_DIR="apps/${FLATHUB_APP_ID}/current"
mkdir -p "$DEST_DIR"
cp -r "${CI_PROJECT_DIR}/flatpak/manifest.json" "$DEST_DIR/manifest.json"

git add "$DEST_DIR/manifest.json"
GIT_AUTHOR_NAME="${GIT_COMMITTER_NAME:-CI Bot}"
GIT_AUTHOR_EMAIL="${GIT_COMMITTER_EMAIL:-ci-bot@example.com}"

git -c user.name="$GIT_AUTHOR_NAME" -c user.email="$GIT_AUTHOR_EMAIL" commit -m "ci: update manifest for ${FLATHUB_APP_ID}"

git push origin "$BRANCH_NAME"

# create PR to upstream
PR_TITLE="ci: update manifest for ${FLATHUB_APP_ID}"
PR_BODY="This PR updates the Flatpak manifest for ${FLATHUB_APP_ID} built by CI. Please review."

API_RESPONSE=$(curl -s -X POST -H "Authorization: token ${GITHUB_TOKEN}" -d "{\"title\": \"${PR_TITLE}\", \"head\": \"${FORK_OWNER}:${BRANCH_NAME}\", \"base\": \"${FLATHUB_TARGET_BRANCH:-master}\", \"body\": \"${PR_BODY}\"}" "https://api.github.com/repos/${UPSTREAM_OWNER}/${UPSTREAM_REPO}/pulls")

echo "PR response: $API_RESPONSE"

echo "Created PR (or attempted). Clean up."
cd /
rm -rf "$TMPDIR"

