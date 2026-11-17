#!/usr/bin/env bash
# Stop hook: before Claude ends a turn, check that changed C# still builds and changed PHP still parses.
# Exit 2 hands the errors back to Claude once; stop_hook_active stops it from looping on the same failure.
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"

input=$(cat)
if printf '%s' "$input" | grep -qE '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
    exit 0
fi

# Files changed against HEAD (staged or not) plus new untracked ones, without build output or ignored paths.
changed=$( { git diff --name-only HEAD; git ls-files --others --exclude-standard; } 2>/dev/null | sort -u )

cs_changed=false
php_files=()
while IFS= read -r file; do
    [ -f "$file" ] || continue
    case "$file" in
        *.cs|*.csproj|*.props|*.slnx) cs_changed=true ;;
        *.php) php_files+=("$file") ;;
    esac
done <<< "$changed"

problems=""

if $cs_changed; then
    build=$(dotnet build InternetShop.slnx --nologo -v q -clp:ErrorsOnly 2>&1) || problems+=$'dotnet build failed:\n'"$(printf '%s\n' "$build" | grep -E 'error' | sort -u | head -20)"$'\n'
fi

for file in "${php_files[@]+"${php_files[@]}"}"; do
    result=$(php -d log_errors=0 -l "$file" 2>&1) || problems+="php -l $file:"$'\n'"$result"$'\n'
done

if [ -n "$problems" ]; then
    printf 'Fix these before finishing:\n%s' "$problems" >&2
    exit 2
fi
