#!/usr/bin/env bash
# Downloads the OpenCart release the shop image is built from into .opencart/ so the editor can resolve
# OpenCart classes (autocomplete, go to definition). Editor aid only; nothing here is used at runtime.
# Usage: tools/fetch-opencart-source.sh
set -euo pipefail

cd "$(dirname "$0")/.."
VERSION="$(sed -n 's/^ARG OPENCART_VERSION=//p' shop/Dockerfile)"
[ -n "$VERSION" ] || { echo "OPENCART_VERSION not found in shop/Dockerfile" >&2; exit 1; }

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
curl -fsSL -o "$tmp/opencart.zip" \
    "https://github.com/opencart/opencart/releases/download/${VERSION}/opencart-${VERSION}.zip"
unzip -q "$tmp/opencart.zip" -d "$tmp/src"

rm -rf .opencart
mv "$tmp/src/upload" .opencart
echo "OpenCart ${VERSION} source is in .opencart/"
