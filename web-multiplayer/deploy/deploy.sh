#!/usr/bin/env bash
# Generic deploy script for the TicTacToe network-multiplayer service.
# All paths are configurable via environment variables. Run on the host
# that will serve the app (typically a small Linux VM/container).
#
# Usage:
#   INSTALL_DIR=/opt/tictactoe-multi ./deploy.sh
#
# Defaults:
#   INSTALL_DIR=/opt/tictactoe-multi
#   REPO_DIR=$INSTALL_DIR/repo
#   PUBLISH_DIR=$INSTALL_DIR/publish
#   SERVICE_NAME=tictactoe-multi   (matches systemd unit)
#
# Prerequisites:
#   - .NET 9 SDK installed (apt: dotnet-sdk-9.0)
#   - git checkout at $REPO_DIR with the desired branch
#   - systemd unit installed (see tictactoe-multi.service.template)
#
set -euo pipefail
: "${INSTALL_DIR:=/opt/tictactoe-multi}"
: "${REPO_DIR:=${INSTALL_DIR}/repo}"
: "${PUBLISH_DIR:=${INSTALL_DIR}/publish}"
: "${SERVICE_NAME:=tictactoe-multi}"

cd "$REPO_DIR"
git pull --ff-only
dotnet publish web-multiplayer -c Release -o "${PUBLISH_DIR}.next"
rm -rf "$PUBLISH_DIR"
mv "${PUBLISH_DIR}.next" "$PUBLISH_DIR"
sudo systemctl restart "$SERVICE_NAME"
echo "deployed: $(git rev-parse --short HEAD)"
