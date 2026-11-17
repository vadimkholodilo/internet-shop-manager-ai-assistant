#!/usr/bin/env bash
# Builds the installable OpenCart extension package (Extensions > Installer > Upload).
# Usage: tools/package-extension.sh    -> dist/ai_assistant.ocmod.zip
set -euo pipefail

cd "$(dirname "$0")/../shop/extension/ai_assistant"
mkdir -p ../../../dist
rm -f ../../../dist/ai_assistant.ocmod.zip
zip -qr ../../../dist/ai_assistant.ocmod.zip . -x '.*'
echo "dist/ai_assistant.ocmod.zip"
