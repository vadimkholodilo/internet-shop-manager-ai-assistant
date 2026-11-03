#!/bin/bash
# Idempotent shop configuration applied after installation: Russian language, rubles, no demo catalog.
set -euo pipefail
WEB="$1"
STATE="$2"
sql() { mariadb -h db -u opencart -p"$SHOP_DB_PASSWORD" opencart --default-character-set=utf8mb4 "$@"; }

echo "[shop] configuring Russian language and rubles"
sql <<'SQL'
UPDATE oc_language SET name = 'Русский', code = 'ru-ru', locale = 'ru-RU,ru_RU.UTF-8,ru' WHERE language_id = 1;
UPDATE oc_currency SET status = 0;
INSERT INTO oc_currency (title, code, symbol_left, symbol_right, decimal_place, value, status, date_modified)
  SELECT 'Российский рубль', 'RUB', '', ' ₽', 0, 1, 1, NOW() FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM oc_currency WHERE code = 'RUB');
UPDATE oc_currency SET title = 'Российский рубль', symbol_left = '', symbol_right = ' ₽', decimal_place = 0, value = 1, status = 1 WHERE code = 'RUB';
UPDATE oc_setting SET value = 'ru-ru' WHERE `key` IN ('config_language_catalog', 'config_language_admin');
UPDATE oc_setting SET value = 'RUB' WHERE `key` = 'config_currency';
UPDATE oc_setting SET value = '0' WHERE `key` = 'config_error_display';
UPDATE oc_setting SET value = 'Электроника' WHERE `key` = 'config_name';
UPDATE oc_setting SET value = '{"1":{"meta_title":"Электроника - тестовый магазин","meta_description":"Тестовый магазин электроники для демонстрации ИИ-ассистента","meta_keyword":""}}' WHERE `key` = 'config_description' AND store_id = 0;
UPDATE oc_stock_status SET name = 'В наличии' WHERE name = 'In Stock';
UPDATE oc_stock_status SET name = 'Нет в наличии' WHERE name = 'Out Of Stock';
UPDATE oc_stock_status SET name = 'Предзаказ' WHERE name = 'Pre-Order';
UPDATE oc_stock_status SET name = '2-3 дня' WHERE name = '2-3 Days';
SQL

# OpenCart ships with an English demo catalog. Remove it once, so the shop contains only loaded data.
if [ ! -f "$STATE/demo-catalog-removed" ]; then
    echo "[shop] removing the OpenCart demo catalog"
    {
        echo "SET FOREIGN_KEY_CHECKS = 0;"
        for t in product product_attribute product_description product_discount product_filter product_image \
                 product_option product_option_value product_related product_report product_reward product_special \
                 product_subscription product_to_category product_to_download product_to_layout product_to_store \
                 product_code product_variant review category category_description category_filter category_path \
                 category_to_layout category_to_store manufacturer manufacturer_to_store attribute attribute_description \
                 attribute_group attribute_group_description; do
            echo "SET @t = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'oc_$t');"
            echo "SET @q = IF(@t > 0, 'TRUNCATE TABLE oc_$t', 'DO 0'); PREPARE s FROM @q; EXECUTE s; DEALLOCATE PREPARE s;"
        done
        echo "DELETE FROM oc_seo_url WHERE \`key\` IN ('product_id', 'path', 'manufacturer_id');"
        echo "SET FOREIGN_KEY_CHECKS = 1;"
    } | sql
    touch "$STATE/demo-catalog-removed"
fi
