#!/usr/bin/env bash
# Stop hook: before Claude ends a turn, check what changed:
#   C# / project files  -> dotnet build
#   PHP files           -> php -l
#   Dockerfiles         -> hadolint, docker build --check, trivy config (HIGH and CRITICAL)
#   docker-compose files-> docker compose config, trivy config
# Exit 2 hands the problems back to Claude once; stop_hook_active stops it from looping on the same failure.
# Linters are optional: a missing tool is skipped. Install them with: brew install hadolint trivy
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"

input=$(cat)
if printf '%s' "$input" | grep -qE '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
    exit 0
fi

# Files changed against HEAD (staged or not) plus new untracked ones, without build output or ignored paths.
changed=$( { git diff --name-only HEAD; git ls-files --others --exclude-standard; } 2>/dev/null | sort -u )

cs_changed=false
compose_changed=false
php_files=()
docker_files=()
while IFS= read -r file; do
    [ -f "$file" ] || continue
    case "$file" in
        *.cs|*.csproj|*.props|*.slnx) cs_changed=true ;;
        *.php) php_files+=("$file") ;;
    esac
    case "${file##*/}" in
        Dockerfile|Dockerfile.*|*.dockerfile) docker_files+=("$file") ;;
        docker-compose*.yml|docker-compose*.yaml|compose.yml|compose.yaml) compose_changed=true ;;
    esac
done <<< "$changed"

problems=""
add_problem() { problems+="$1"$'\n'"$2"$'\n'; }

# --- C# -------------------------------------------------------------------------------------------
if $cs_changed; then
    build=$(dotnet build InternetShop.slnx --nologo -v q -clp:ErrorsOnly 2>&1) \
        || add_problem "dotnet build failed:" "$(printf '%s\n' "$build" | grep -E 'error' | sort -u | head -20)"
fi

# --- PHP ------------------------------------------------------------------------------------------
for file in "${php_files[@]+"${php_files[@]}"}"; do
    result=$(php -d log_errors=0 -l "$file" 2>&1) || add_problem "php -l $file:" "$result"
done

# --- Docker ---------------------------------------------------------------------------------------
# The build context is the directory of the Dockerfile unless the Dockerfile says otherwise.
build_context() {
    case "$1" in
        src/ShopLoader/Dockerfile) echo . ;;
        *) dirname "$1" ;;
    esac
}

for file in "${docker_files[@]+"${docker_files[@]}"}"; do
    if command -v hadolint >/dev/null 2>&1; then
        result=$(hadolint --no-color --failure-threshold warning "$file" 2>&1) || add_problem "hadolint $file:" "$result"
    fi

    # BuildKit's own checks (needs the docker daemon and registry access; an environment failure is not a finding).
    if docker info >/dev/null 2>&1; then
        result=$(docker build --check -f "$file" "$(build_context "$file")" 2>&1)
        if printf '%s' "$result" | grep -qE '^WARNING:|dockerfile parse error'; then
            add_problem "docker build --check $file:" "$(printf '%s\n' "$result" | grep -vE '^#[0-9]+ ' | head -20)"
        fi
    fi
done

if $compose_changed && docker info >/dev/null 2>&1; then
    result=$(docker compose -f docker-compose.yml config -q 2>&1) \
        || add_problem "docker compose config (docker-compose.yml):" "$result"
    if [ -f docker-compose.dev.yml ]; then
        result=$(docker compose -f docker-compose.yml -f docker-compose.dev.yml config -q 2>&1) \
            || add_problem "docker compose config (with docker-compose.dev.yml):" "$result"
    fi
fi

if { [ "${#docker_files[@]}" -gt 0 ] || $compose_changed; } && command -v trivy >/dev/null 2>&1; then
    # Accepted findings are listed with reasons in .trivyignore.yaml.
    result=$(trivy config --quiet --severity HIGH,CRITICAL --ignorefile .trivyignore.yaml --skip-version-check \
        --skip-dirs .opencart --skip-dirs bin --skip-dirs obj --exit-code 1 . 2>&1) \
        || add_problem "trivy config (HIGH and CRITICAL misconfigurations; accept one in .trivyignore.yaml only with a reason):" "$(printf '%s\n' "$result" | head -40)"
fi

if [ -n "$problems" ]; then
    printf 'Fix these before finishing:\n%s' "$problems" >&2
    exit 2
fi
