#!/usr/bin/env bash
# Pulls the latest code and rebuilds/restarts the stack on the server.
# Run by the GitHub Actions workflow over SSH, but also safe to run by hand.
set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/InnovatorBackend}"
BRANCH="${BRANCH:-main}"

cd "$APP_DIR"

echo "==> Fetching latest ($BRANCH)"
git fetch --all --prune
git reset --hard "origin/$BRANCH"

echo "==> Rebuilding and restarting containers"
docker compose up -d --build --remove-orphans

echo "==> Cleaning up dangling images"
docker image prune -f >/dev/null 2>&1 || true

echo "==> Current status"
docker compose ps
echo "Deploy complete."
