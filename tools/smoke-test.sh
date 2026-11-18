#!/usr/bin/env bash
# Starts the demo exactly as a reader would (docker compose up -d) and checks that the seeded shop is up.
# Usage: tools/smoke-test.sh [--keep]    (--keep leaves the stack running afterwards)
set -euo pipefail

cd "$(dirname "$0")/.."
[ -f .env ] || cp .env.example .env
set -a; . ./.env; set +a
PORT="${SHOP_PORT:-8080}"
KEY="${SHOP_API_KEY:-local-demo-api-key-change-me}"

cleanup() {
    status=$?
    if [ "$status" -ne 0 ]; then docker compose logs --tail 80 || true; fi
    if [ "${1:-}" != "--keep" ]; then docker compose down -v >/dev/null 2>&1 || true; fi
    exit "$status"
}
trap 'cleanup "${1:-}"' EXIT

docker compose down -v >/dev/null 2>&1 || true
start=$(date +%s)
docker compose up -d --build

echo "waiting for the loader to finish"
for _ in $(seq 1 120); do
    state=$(docker compose ps -a loader --format '{{.State}}')
    [ "$state" = "exited" ] && break
    sleep 2
done
code=$(docker compose ps -a loader --format '{{.ExitCode}}')
[ "$code" = "0" ] || { echo "loader exited with code $code"; exit 1; }
echo "seeded in $(( $(date +%s) - start )) seconds"

total=$(curl -fsS -H "Authorization: Bearer $KEY" "http://localhost:$PORT/catalog-api/products" | grep -o '"total":[0-9]*' | cut -d: -f2)
[ "${total:-0}" -ge 20 ] || { echo "expected at least 20 products, found ${total:-0}"; exit 1; }

home=$(curl -fsS "http://localhost:$PORT/")
grep -q 'lang="ru"' <<<"$home" || { echo "storefront is not in Russian"; exit 1; }
results=$(curl -fsS "http://localhost:$PORT/index.php?route=product/search&search=Norda&language=ru-ru")
grep -q '₽' <<<"$results" || { echo "no prices in rubles"; exit 1; }

echo "second 'docker compose up -d' must create nothing new"
docker compose up -d
for _ in $(seq 1 60); do
    [ "$(docker compose ps -a loader --format '{{.State}}')" = "exited" ] && break
    sleep 2
done
docker compose logs loader | tail -3 | grep -q 'Products: 0 created' || { echo "second run created products"; exit 1; }
[ "$(curl -fsS -H "Authorization: Bearer $KEY" "http://localhost:$PORT/catalog-api/products" | grep -o '"total":[0-9]*' | cut -d: -f2)" = "$total" ] || { echo "product count changed"; exit 1; }

echo "OK: $total products, Russian storefront, idempotent re-run"
