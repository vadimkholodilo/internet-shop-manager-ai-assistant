#!/usr/bin/env bash
# Stop hook: before Claude ends a turn, run the shared checks (tools/check-changed.sh) on everything changed since
# HEAD. Exit 2 hands the problems back to Claude once; stop_hook_active stops it from looping on the same failure.
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"

input=$(cat)
if printf '%s' "$input" | grep -qE '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
    exit 0
fi

# Changed against HEAD (staged or not) plus new untracked files; ignored paths such as build output are left out.
{ git diff --name-only HEAD; git ls-files --others --exclude-standard; } 2>/dev/null | sort -u | tools/check-changed.sh || exit 2
