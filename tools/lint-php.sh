#!/usr/bin/env bash
# Syntax-checks every tracked PHP file with `php -l`, using the PHP version the shop image runs (read from
# shop/Dockerfile) so the check matches production. It needs no OpenCart sources: a syntax check parses each
# file on its own and never resolves classes.
# Usage: tools/lint-php.sh
set -euo pipefail

cd "$(dirname "$0")/.."
version="$(sed -n 's/^FROM php:\([0-9.]*\)-apache.*/\1/p' shop/Dockerfile)"
[ -n "$version" ] || { echo "PHP version not found in shop/Dockerfile" >&2; exit 1; }

git ls-files -z '*.php' | xargs -0 docker run --rm -v "$PWD":/app:ro -w /app "php:${version}-cli" sh -c '
    status=0
    for file; do
        output=$(php -d display_errors=1 -d log_errors=0 -l "$file" 2>&1) || { echo "$output"; status=1; }
    done
    exit $status
' sh
echo "PHP syntax OK ($(git ls-files '*.php' | wc -l | tr -d ' ') files, PHP ${version})"
