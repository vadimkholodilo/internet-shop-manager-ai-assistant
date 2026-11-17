#!/usr/bin/env bash
# Builds the installable OpenCart extension package (Extensions > Installer > Upload).
# Usage: tools/package-extension.sh    -> dist/internet_shop.ocmod.zip
set -euo pipefail

cd "$(dirname "$0")/../shop/extension/internet_shop"
mkdir -p ../../../dist
rm -f ../../../dist/internet_shop.ocmod.zip
zip -qr ../../../dist/internet_shop.ocmod.zip . -x '.*'
echo "dist/internet_shop.ocmod.zip"
