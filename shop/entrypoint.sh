#!/bin/bash
# Unattended OpenCart provisioning. Every step is safe to re-run.
set -euo pipefail

WEB=/var/www/html
STATE=/state
DB_HOST=db
DB_NAME=opencart
DB_USER=opencart
DB_PASS="${SHOP_DB_PASSWORD:?SHOP_DB_PASSWORD is required}"
PORT="${SHOP_PORT:-8080}"

log() { echo "[shop] $*"; }

sql() { mariadb -h "$DB_HOST" -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" --default-character-set=utf8mb4 "$@"; }

if [ "${1:-}" != "apache2-foreground" ]; then
    exec "$@"
fi

log "waiting for the database"
for _ in $(seq 1 60); do
    if sql -e 'SELECT 1' >/dev/null 2>&1; then break; fi
    sleep 2
done
sql -e 'SELECT 1' >/dev/null 2>&1 || { log "ERROR: database is not reachable on $DB_HOST"; exit 1; }

link_config() {
    ln -sf "$STATE/config.php" "$WEB/config.php"
    ln -sf "$STATE/admin-config.php" "$WEB/admin/config.php"
}

if [ -f "$STATE/config.php" ] && [ -f "$STATE/admin-config.php" ]; then
    log "existing installation found, skipping install"
    link_config
else
    log "installing OpenCart"
    cp "$WEB/config-dist.php" "$WEB/config.php"
    cp "$WEB/admin/config-dist.php" "$WEB/admin/config.php"
    php "$WEB/install/cli_install.php" install \
        --username "${SHOP_ADMIN_USER:-admin}" \
        --password "${SHOP_ADMIN_PASSWORD:-admin}" \
        --email "${SHOP_ADMIN_EMAIL:-admin@example.com}" \
        --http_server "http://localhost:${PORT}/" \
        --language en-gb \
        --db_driver mysqli \
        --db_hostname "$DB_HOST" \
        --db_username "$DB_USER" \
        --db_password "$DB_PASS" \
        --db_database "$DB_NAME" \
        --db_port 3306 \
        --db_prefix oc_
    mv "$WEB/config.php" "$STATE/config.php"
    mv "$WEB/admin/config.php" "$STATE/admin-config.php"
    link_config
fi

# The installer directory must not stay reachable after installation.
rm -rf "$WEB/install"

/usr/local/bin/shop-provision "$WEB" "$STATE"

chown -R www-data:www-data "$WEB/image" "$WEB/system/storage" "$STATE"
touch "$STATE/ready"
log "ready"
exec "$@"
