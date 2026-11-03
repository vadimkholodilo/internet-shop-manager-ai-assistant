<?php
// Build-time helper: overlays Russian strings onto the ru-ru copy of OpenCart's en-gb language files.
// Keys that are not translated keep their English text, so no key is ever missing.
// Usage: php apply.php <opencart root> <translations.php>

[, $root, $translations_file] = $argv;
$translations = require $translations_file;
$sort_texts = [
    'text_sort' => 'Сортировка', 'text_default' => 'По умолчанию', 'text_name_asc' => 'Название (А - Я)',
    'text_name_desc' => 'Название (Я - А)', 'text_price_asc' => 'Цена (по возрастанию)',
    'text_price_desc' => 'Цена (по убыванию)', 'text_rating_asc' => 'Рейтинг (по возрастанию)',
    'text_rating_desc' => 'Рейтинг (по убыванию)', 'text_model_asc' => 'Артикул (А - Я)',
    'text_model_desc' => 'Артикул (Я - А)', 'text_limit' => 'Показывать', 'text_compare' => 'Сравнение товаров (%s)',
];
foreach (['product/category', 'product/search', 'product/manufacturer', 'product/special'] as $file) {
    $translations[$file] = ($translations[$file] ?? []) + $sort_texts;
}

foreach ($translations as $file => $strings) {
    $path = $root . '/catalog/language/ru-ru/' . $file . '.php';
    if (!is_file($path)) {
        fwrite(STDERR, "missing language file: $path\n");
        exit(1);
    }
    $content = file_get_contents($path);
    foreach ($strings as $key => $value) {
        $pattern = '/^(\$_\[\'' . preg_quote($key, '/') . '\'\]\s*=\s*)(\'(?:[^\'\\\\]|\\\\.)*\'|"(?:[^"\\\\]|\\\\.)*");/m';
        $quoted = "'" . str_replace(['\\', "'"], ['\\\\', "\\'"], $value) . "'";
        $count = 0;
        $content = preg_replace_callback($pattern, fn ($m) => $m[1] . $quoted . ';', $content, 1, $count);
        if ($count === 0) {
            fwrite(STDERR, "key not found: $file $key\n");
            exit(1);
        }
    }
    file_put_contents($path, $content);
}
