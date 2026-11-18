#!/usr/bin/env bash
# Checks the files whose paths are read from stdin (one per line) and reports what is wrong on stderr.
#   C# / project files   -> dotnet build
#   PHP files            -> php -l
#   Dockerfiles          -> hadolint, docker build --check, trivy config (HIGH and CRITICAL)
#   docker-compose files -> docker compose config, trivy config
# One script for every caller, so quality does not depend on who edits: .githooks/pre-commit (staged files) and
# .claude/hooks/check-code.sh (files Claude changed). Exit 1 means problems were found.
# The linters are optional: a missing one is reported and skipped. Install them with: brew install hadolint trivy
# Usage: git diff --cached --name-only | tools/check-changed.sh
set -uo pipefail

cd "$(git rev-parse --show-toplevel)"

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
done

problems=""
add_problem() { problems+="$1"$'\n'"$2"$'\n'; }
skipped() { echo "check-changed: $1 is not installed, skipping it (brew install hadolint trivy)" >&2; }

# --- C# -------------------------------------------------------------------------------------------
if $cs_changed; then
    build=$(dotnet build InternetShop.slnx --nologo -v q -clp:ErrorsOnly 2>&1 </dev/null) \
        || add_problem "dotnet build failed:" "$(printf '%s\n' "$build" | grep -E 'error' | sort -u | head -20)"
fi

# --- PHP ------------------------------------------------------------------------------------------
for file in "${php_files[@]+"${php_files[@]}"}"; do
    result=$(php -d log_errors=0 -l "$file" 2>&1 </dev/null) || add_problem "php -l $file:" "$result"
done

# --- Docker ---------------------------------------------------------------------------------------
# The build context is the directory of the Dockerfile unless the Dockerfile says otherwise.
build_context() {
    case "$1" in
        src/ShopLoader/Dockerfile) echo . ;;
        *) dirname "$1" ;;
    esac
}

docker_available=false
docker info >/dev/null 2>&1 && docker_available=true

for file in "${docker_files[@]+"${docker_files[@]}"}"; do
    if command -v hadolint >/dev/null 2>&1; then
        result=$(hadolint --no-color --failure-threshold warning "$file" 2>&1 </dev/null) || add_problem "hadolint $file:" "$result"
    else
        skipped hadolint
    fi

    # BuildKit's own checks (needs the docker daemon and registry access; an environment failure is not a finding).
    if $docker_available; then
        result=$(docker build --check -f "$file" "$(build_context "$file")" 2>&1 </dev/null)
        if printf '%s' "$result" | grep -qE '^WARNING:|dockerfile parse error'; then
            add_problem "docker build --check $file:" "$(printf '%s\n' "$result" | grep -vE '^#[0-9]+ ' | head -20)"
        fi
    fi
done

if $compose_changed && $docker_available; then
    result=$(docker compose -f docker-compose.yml config -q 2>&1 </dev/null) \
        || add_problem "docker compose config (docker-compose.yml):" "$result"
    if [ -f docker-compose.dev.yml ]; then
        result=$(docker compose -f docker-compose.yml -f docker-compose.dev.yml config -q 2>&1 </dev/null) \
            || add_problem "docker compose config (with docker-compose.dev.yml):" "$result"
    fi
fi

if [ "${#docker_files[@]}" -gt 0 ] || $compose_changed; then
    if command -v trivy >/dev/null 2>&1; then
        # Scans the whole repository on purpose: scanning one file changes the reported paths, so the accepted findings in
        # .trivyignore.yaml (listed with reasons, matched by path) would stop matching.
        result=$(trivy config --quiet --severity HIGH,CRITICAL --ignorefile .trivyignore.yaml --skip-version-check \
            --skip-dirs .opencart --skip-dirs bin --skip-dirs obj --exit-code 1 . 2>&1 </dev/null) \
            || add_problem "trivy config (HIGH and CRITICAL misconfigurations; accept one in .trivyignore.yaml only with a reason):" "$(printf '%s\n' "$result" | head -40)"
    else
        skipped trivy
    fi
fi

if [ -n "$problems" ]; then
    printf 'Checks failed:\n%s' "$problems" >&2
    exit 1
fi
