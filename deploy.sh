#!/usr/bin/env bash
# Builds the mod and installs it into the game's mods directory.
#
# Usage: ./deploy.sh [path to "Slay the Spire 2"]
set -euo pipefail

GAME_DIR="${1:-/mnt/t/SteamLibrary/steamapps/common/Slay the Spire 2}"
MOD_ID="EtherealGlow"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

if [[ ! -f "$GAME_DIR/data_sts2_windows_x86_64/sts2.dll" ]]; then
    echo "error: no StS2 install at '$GAME_DIR'" >&2
    exit 1
fi

TARGET="$GAME_DIR/mods/$MOD_ID"

"$DOTNET" build "$PROJECT_DIR/EtherealGlow.csproj" -c Release -p:Sts2Dir="$GAME_DIR"

mkdir -p "$TARGET"
cp "$PROJECT_DIR/bin/Release/net9.0/$MOD_ID.dll" "$TARGET/"
cp "$PROJECT_DIR/$MOD_ID.json" "$TARGET/"

# Never clobber a config the player has already tuned.
if [[ ! -f "$TARGET/$MOD_ID.config.jsonc" ]]; then
    cp "$PROJECT_DIR/$MOD_ID.config.jsonc" "$TARGET/"
fi

# The game parses every *.json under mods/ as a manifest, so the old config name
# logs an error on every launch. Retire it once its replacement is in place.
if [[ -f "$TARGET/$MOD_ID.config.json" && -f "$TARGET/$MOD_ID.config.jsonc" ]]; then
    rm -f "$TARGET/$MOD_ID.config.json"
    echo "Removed the superseded $MOD_ID.config.json (settings now live in .jsonc)"
fi

echo "Installed $MOD_ID to $TARGET"
ls -la "$TARGET"
