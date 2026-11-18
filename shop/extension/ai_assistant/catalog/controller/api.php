<?php
namespace Opencart\Catalog\Controller\Extension\AiAssistant;

/**
 * Catalog REST API for OpenCart.
 *
 * Contract: specs/001-test-shop/contracts/catalog-endpoint.md
 *
 * Reads and writes go through OpenCart's own admin models, so product, category, brand and attribute
 * data is stored exactly as the admin area would store it. Items are addressed by stable keys
 * (category slug, product SKU); responses also carry the shop's numeric ids.
 */
class Api extends \Opencart\System\Engine\Controller {
    private const ATTRIBUTE_GROUP = 'Характеристики';
    private const PAGE_SIZE = 50;

    /** Product columns this API does not manage; an update keeps whatever a manager set in the admin area. */
    private const UNMANAGED_PRODUCT_FIELDS = [
        'sku', 'upc', 'ean', 'jan', 'isbn', 'mpn', 'location', 'minimum', 'subtract', 'stock_status_id',
        'date_available', 'shipping', 'points', 'weight', 'weight_class_id', 'length', 'width', 'height',
        'length_class_id', 'status', 'tax_class_id', 'sort_order',
    ];

    private int $language_id = 0;

    public function index(): void {
        $this->language_id = (int)$this->config->get('config_language_id');

        if (!$this->authorised()) {
            $this->respond(401, ['error' => 'Missing or invalid API key']);
            return;
        }

        $this->autoloader->register('Opencart\Admin', DIR_OPENCART . 'admin/');

        // Admin models call each other through $this->load->model(). Register the admin versions up front,
        // otherwise the catalog application resolves same-named catalog models instead.
        foreach (['design/seo_url', 'catalog/category', 'catalog/product', 'catalog/manufacturer', 'catalog/attribute', 'catalog/attribute_group'] as $route) {
            $this->admin($route);
        }

        $write = ($_SERVER['REQUEST_METHOD'] ?? 'GET') !== 'GET';

        $lock = null;
        try {
            // Writes to one item are done one at a time. Without this, a repeated write (a client retry sent while the
            // first request is still running) could find no item and create it a second time.
            if ($write) {
                $name = $this->writeLockName();
                if ($name !== null) {
                    $this->acquireLock($name);
                    $lock = $name;
                }
            }
            // A failed write must not leave half-created records behind.
            if ($write) {
                $this->db->query("START TRANSACTION");
            }
            $this->dispatch();
            if ($write) {
                $this->db->query("COMMIT");
            }
        } catch (ApiError $e) {
            if ($write) {
                $this->db->query("ROLLBACK");
            }
            $this->respond($e->status, ['error' => $e->getMessage()]);
        } catch (\Throwable $e) {
            if ($write) {
                $this->db->query("ROLLBACK");
            }
            $this->log->write('catalog_api: ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine());
            $this->respond(500, ['error' => 'Internal error']);
        } finally {
            if ($lock !== null) {
                $this->db->query("SELECT RELEASE_LOCK('" . $this->db->escape($lock) . "')");
            }
        }
    }

    /** Name of the lock for the item a write is about, or null when the request is not a write to an item. */
    private function writeLockName(): ?string {
        $parts = array_values(array_filter(explode('/', trim((string)($this->request->get['api_path'] ?? ''), '/')), 'strlen'));
        $parts = array_map('rawurldecode', $parts);
        $resource = $parts[0] ?? '';
        $key = $parts[1] ?? null;
        if ($key === null) {
            return null;
        }

        // The same normalization that makes the URL keyword, so SKUs that would collide on it share one lock.
        if ($resource === 'products') {
            return 'catalog_api:' . md5('product:' . $this->productKeyword($key));
        }
        if ($resource === 'categories') {
            return 'catalog_api:' . md5('category:' . $this->keyword($key, 'category'));
        }
        return null;
    }

    private function acquireLock(string $name): void {
        $row = $this->db->query("SELECT GET_LOCK('" . $this->db->escape($name) . "', 10) AS `locked`")->row;
        if ((int)($row['locked'] ?? 0) !== 1) {
            // Another request keeps writing this item; the client repeats the request after a pause.
            throw new ApiError(503, 'The item is being written by another request, try again');
        }
    }

    // ---------------------------------------------------------------- routing

    private function dispatch(): void {
        $method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
        $parts = array_values(array_filter(explode('/', trim((string)($this->request->get['api_path'] ?? ''), '/')), 'strlen'));
        $parts = array_map('rawurldecode', $parts);

        $resource = $parts[0] ?? '';
        $key = $parts[1] ?? null;
        $sub = $parts[2] ?? null;

        if ($resource === 'categories') {
            if ($method === 'GET' && $key === null) {
                $this->listCategories();
            } elseif ($method === 'GET' && $key === 'by-id' && $sub !== null) {
                $this->respond(200, $this->categoryRepresentation($this->categoryByIdOr404((int)$sub)));
            } elseif ($method === 'GET' && $key !== null) {
                $this->respond(200, $this->categoryRepresentation($this->categoryBySlugOr404($key)));
            } elseif ($method === 'PUT' && $key !== null) {
                $this->upsertCategory($key);
            } else {
                throw new ApiError(405, 'Method not allowed');
            }
            return;
        }

        if ($resource === 'products') {
            if ($method === 'GET' && $key === null) {
                $this->listProducts();
            } elseif ($method === 'GET' && $key === 'by-id' && $sub !== null) {
                $this->respond(200, $this->productRepresentation($this->productByIdOr404((int)$sub)));
            } elseif ($method === 'GET' && $key !== null && $sub === null) {
                $this->respond(200, $this->productRepresentation($this->productBySkuOr404($key)));
            } elseif ($method === 'PUT' && $key !== null && $sub === null) {
                $this->upsertProduct($key);
            } elseif ($method === 'PUT' && $key !== null && $sub === 'image') {
                $this->uploadImage($key);
            } else {
                throw new ApiError(405, 'Method not allowed');
            }
            return;
        }

        throw new ApiError(404, 'Unknown resource');
    }

    // ------------------------------------------------------------------- auth

    private function authorised(): bool {
        $expected = (string)getenv('SHOP_API_KEY');
        $header = $_SERVER['HTTP_AUTHORIZATION'] ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] ?? '';

        if ($expected === '' || !preg_match('/^Bearer\s+(.+)$/i', $header, $m)) {
            return false;
        }

        return hash_equals($expected, trim($m[1]));
    }

    // ------------------------------------------------------------ admin models

    /** Loads an OpenCart admin model into the catalog application and returns it. */
    private function admin(string $route): object {
        $key = 'model_' . str_replace('/', '_', $route);

        if (!$this->registry->has('fallback_' . $key)) {
            $class = 'Opencart\\Admin\\Model\\' . str_replace(['_', '/'], ['', '\\'], ucwords($route, '_/'));
            $this->registry->set('fallback_' . $key, new $class($this->registry));
        }

        $this->load->model($route);

        return $this->registry->get($key);
    }

    // -------------------------------------------------------------- categories

    private function upsertCategory(string $slug): void {
        $this->assertSlug($slug);
        $body = $this->body();
        $name = $this->requiredString($body, 'name');
        $parent_slug = isset($body['parent']) && $body['parent'] !== '' ? (string)$body['parent'] : null;

        $parent_id = 0;
        if ($parent_slug !== null) {
            $parent = $this->findCategoryBySlug($parent_slug);
            if ($parent === null) {
                throw new ApiError(404, 'Unknown parent category: ' . $parent_slug);
            }
            $parent_id = (int)$parent['category_id'];
        }

        $model = $this->admin('catalog/category');
        $existing = $this->findCategoryBySlug($slug);
        $description = [
            'name' => $this->encode($name),
            'description' => '',
            'meta_title' => $this->encode($name),
            'meta_description' => '',
            'meta_keyword' => '',
        ];

        if ($existing === null) {
            $category_id = $model->addCategory([
                'image' => '',
                'parent_id' => $parent_id,
                'sort_order' => 0,
                'status' => 1,
                'top' => $parent_id === 0 ? 1 : 0,
                'column' => 1,
                'category_description' => [$this->language_id => $description],
                'category_store' => [0],
                'category_seo_url' => [0 => [$this->language_id => $slug]],
            ]);
            $created = true;
        } else {
            $category_id = (int)$existing['category_id'];
            $current = $model->getCategory($category_id);
            $model->editCategory($category_id, [
                'image' => (string)$current['image'],
                'parent_id' => $parent_id,
                'sort_order' => (int)$current['sort_order'],
                'status' => (int)$current['status'],
                'top' => (int)($current['top'] ?? 0),
                'column' => (int)($current['column'] ?? 1),
                'category_description' => [$this->language_id => $description],
                'category_store' => [0],
                'category_seo_url' => [0 => [$this->language_id => $slug]],
            ]);
            $created = false;
        }

        $this->respond(200, $this->categoryRepresentation($this->categoryByIdOr404($category_id)) + ['created' => $created]);
    }

    private function listCategories(): void {
        $page = max(1, (int)($this->request->get['page'] ?? 1));
        $total = (int)$this->db->query("SELECT COUNT(*) AS `total` FROM `" . DB_PREFIX . "category`")->row['total'];
        $rows = $this->db->query("SELECT `category_id` FROM `" . DB_PREFIX . "category` ORDER BY `category_id` LIMIT " . (($page - 1) * self::PAGE_SIZE) . ", " . self::PAGE_SIZE)->rows;

        $items = [];
        foreach ($rows as $row) {
            $items[] = $this->categoryRepresentation($this->categoryByIdOr404((int)$row['category_id']));
        }

        $this->respond(200, ['items' => $items, 'total' => $total]);
    }

    private function findCategoryBySlug(string $slug): ?array {
        $row = $this->db->query("SELECT `value` FROM `" . DB_PREFIX . "seo_url` WHERE `key` = 'path' AND `store_id` = '0' AND `language_id` = '" . $this->language_id . "' AND (`keyword` = '" . $this->db->escape($slug) . "' OR `keyword` LIKE '%/" . $this->db->escape(addcslashes($slug, '%_')) . "') LIMIT 1")->row;

        if (!$row) {
            return null;
        }

        $parts = explode('_', (string)$row['value']);

        return $this->categoryRow((int)end($parts));
    }

    private function categoryRow(int $category_id): ?array {
        $row = $this->admin('catalog/category')->getCategory($category_id);

        return $row ? $row : null;
    }

    private function categoryBySlugOr404(string $slug): array {
        return $this->findCategoryBySlug($slug) ?? throw new ApiError(404, 'Category not found');
    }

    private function categoryByIdOr404(int $id): array {
        return $this->categoryRow($id) ?? throw new ApiError(404, 'Category not found');
    }

    private function slugOfCategory(int $category_id): ?string {
        $path = $this->admin('catalog/category')->getPath($category_id);
        $row = $this->db->query("SELECT `keyword` FROM `" . DB_PREFIX . "seo_url` WHERE `key` = 'path' AND `value` = '" . $this->db->escape((string)$path) . "' AND `store_id` = '0' AND `language_id` = '" . $this->language_id . "' LIMIT 1")->row;

        if (!$row) {
            return null;
        }

        $parts = explode('/', (string)$row['keyword']);

        return end($parts);
    }

    private function categoryRepresentation(array $category): array {
        $id = (int)$category['category_id'];
        $parent_id = (int)$category['parent_id'];
        $descriptions = $this->admin('catalog/category')->getDescriptions($id);

        return [
            'id' => $id,
            'slug' => $this->slugOfCategory($id),
            'name' => $this->decode($descriptions[$this->language_id]['name'] ?? ''),
            'parent_id' => $parent_id ?: null,
            'parent_slug' => $parent_id ? $this->slugOfCategory($parent_id) : null,
        ];
    }

    // ---------------------------------------------------------------- products

    private function upsertProduct(string $sku): void {
        $body = $this->body();
        $name = $this->requiredString($body, 'name');
        $brand = $this->requiredString($body, 'brand');
        $description = $this->requiredString($body, 'description');
        $category_slug = $this->requiredString($body, 'category');

        if (!isset($body['price']) || !is_numeric($body['price']) || (float)$body['price'] <= 0) {
            throw new ApiError(400, 'price must be a number greater than 0');
        }
        if (!isset($body['quantity']) || !is_int($body['quantity']) || $body['quantity'] < 0) {
            throw new ApiError(400, 'quantity must be an integer, 0 or more');
        }

        $attributes = $body['attributes'] ?? [];
        if (!is_array($attributes)) {
            throw new ApiError(400, 'attributes must be an object');
        }

        $category = $this->findCategoryBySlug($category_slug);
        if ($category === null) {
            throw new ApiError(404, 'Unknown category: ' . $category_slug);
        }

        $model = $this->admin('catalog/product');
        $manufacturer_id = $this->ensureManufacturer($brand);
        $product_attributes = $this->ensureAttributes($attributes);

        $existing = $this->findProductBySku($sku);
        $this->assertKeywordIsFree($sku, $existing === null ? null : (int)$existing['product_id']);

        $data = [
            'model' => $sku,
            'sku' => '',
            'upc' => '',
            'ean' => '',
            'jan' => '',
            'isbn' => '',
            'mpn' => '',
            'location' => '',
            'variant' => [],
            'override' => [],
            'master_id' => 0,
            'quantity' => (int)$body['quantity'],
            'minimum' => 1,
            'subtract' => 1,
            'stock_status_id' => 5,
            'date_available' => date('Y-m-d'),
            'manufacturer_id' => $manufacturer_id,
            'shipping' => 1,
            'price' => (float)$body['price'],
            'points' => 0,
            'weight' => 0,
            'weight_class_id' => 1,
            'length' => 0,
            'width' => 0,
            'height' => 0,
            'length_class_id' => 1,
            'status' => 1,
            'tax_class_id' => 0,
            'sort_order' => 0,
            'image' => '',
            'product_description' => [$this->language_id => [
                'name' => $this->encode($name),
                'description' => $this->encodeDescription($description),
                'tag' => '',
                'meta_title' => $this->encode($name),
                'meta_description' => $this->encode(mb_substr($description, 0, 160)),
                'meta_keyword' => '',
            ]],
            'product_category' => [(int)$category['category_id']],
            'product_store' => [0],
            'product_attribute' => $product_attributes,
            'product_seo_url' => [0 => [$this->language_id => $this->productKeyword($sku)]],
        ];

        if ($existing === null) {
            $product_id = $model->addProduct($data);
            $created = true;
        } else {
            $product_id = (int)$existing['product_id'];
            // Keep what this API does not manage: the photos and the settings a manager may have changed.
            foreach (self::UNMANAGED_PRODUCT_FIELDS as $field) {
                $data[$field] = $existing[$field] ?? $data[$field];
            }
            $data['image'] = (string)$existing['image'];
            $data['product_image'] = $model->getImages($product_id);
            $model->editProduct($product_id, $data);
            $created = false;
        }

        $this->respond(200, $this->productRepresentation($this->productByIdOr404($product_id)) + ['created' => $created]);
    }

    private function uploadImage(string $sku): void {
        $product = $this->productBySkuOr404($sku);

        $type = strtolower(trim(explode(';', $_SERVER['CONTENT_TYPE'] ?? '')[0]));
        $extensions = ['image/jpeg' => 'jpg', 'image/png' => 'png', 'image/webp' => 'webp'];
        if (!isset($extensions[$type])) {
            throw new ApiError(415, 'Image must be image/jpeg, image/png or image/webp');
        }

        $bytes = file_get_contents('php://input');
        if ($bytes === false || $bytes === '' || @getimagesizefromstring($bytes) === false) {
            throw new ApiError(400, 'Body is not a valid image');
        }

        $relative = 'catalog/products/' . $this->productKeyword($sku) . '.' . $extensions[$type];
        $directory = DIR_IMAGE . 'catalog/products/';
        if (!is_dir($directory) && !mkdir($directory, 0775, true) && !is_dir($directory)) {
            throw new ApiError(500, 'Could not create image directory');
        }
        if (file_put_contents(DIR_IMAGE . $relative, $bytes) === false) {
            throw new ApiError(500, 'Could not store image');
        }

        $model = $this->admin('catalog/product');
        $product_id = (int)$product['product_id'];
        $data = $this->productEditData($product_id, $product);
        $data['image'] = $relative;
        $model->editProduct($product_id, $data);

        $this->respond(200, $this->productRepresentation($this->productByIdOr404($product_id)));
    }

    /** Rebuilds the full data array the admin model expects when editing an existing product. */
    private function productEditData(int $product_id, array $product): array {
        $model = $this->admin('catalog/product');
        $data = $product;
        $data['variant'] = [];
        $data['override'] = [];
        $data['product_description'] = $model->getDescriptions($product_id);
        $data['product_category'] = $model->getCategories($product_id);
        $data['product_store'] = $model->getStores($product_id);
        $data['product_attribute'] = $model->getAttributes($product_id);
        $data['product_image'] = $model->getImages($product_id);
        $data['product_seo_url'] = [0 => [$this->language_id => $this->productKeyword((string)$product['model'])]];

        return $data;
    }

    private function listProducts(): void {
        $page = max(1, (int)($this->request->get['page'] ?? 1));
        $total = (int)$this->db->query("SELECT COUNT(*) AS `total` FROM `" . DB_PREFIX . "product`")->row['total'];
        $rows = $this->db->query("SELECT `product_id` FROM `" . DB_PREFIX . "product` ORDER BY `product_id` LIMIT " . (($page - 1) * self::PAGE_SIZE) . ", " . self::PAGE_SIZE)->rows;

        $items = [];
        foreach ($rows as $row) {
            $items[] = $this->productRepresentation($this->productByIdOr404((int)$row['product_id']));
        }

        $this->respond(200, ['items' => $items, 'total' => $total]);
    }

    private function findProductBySku(string $sku): ?array {
        $row = $this->db->query("SELECT `product_id` FROM `" . DB_PREFIX . "product` WHERE `model` = '" . $this->db->escape($sku) . "' ORDER BY `product_id` LIMIT 1")->row;

        return $row ? $this->productByIdOr404((int)$row['product_id']) : null;
    }

    private function productBySkuOr404(string $sku): array {
        return $this->findProductBySku($sku) ?? throw new ApiError(404, 'Product not found');
    }

    private function productByIdOr404(int $id): array {
        $row = $this->admin('catalog/product')->getProduct($id);

        return $row ? $row : throw new ApiError(404, 'Product not found');
    }

    /** URL keyword (and image file name) for a SKU. */
    private function productKeyword(string $sku): string {
        return $this->keyword($sku, 'product');
    }

    private function keyword(string $text, string $fallbackPrefix): string {
        $keyword = trim((string)preg_replace('/[^a-z0-9]+/', '-', strtolower($text)), '-');

        return $keyword !== '' ? $keyword : $fallbackPrefix . '-' . substr(md5($text), 0, 8);
    }

    /** Different SKUs can map to the same keyword (AB_1 and AB-1); they would share a URL and a photo. */
    private function assertKeywordIsFree(string $sku, ?int $own_product_id): void {
        $row = $this->db->query("SELECT `value` FROM `" . DB_PREFIX . "seo_url` WHERE `key` = 'product_id' AND `store_id` = '0' AND `language_id` = '" . $this->language_id . "' AND `keyword` = '" . $this->db->escape($this->productKeyword($sku)) . "' LIMIT 1")->row;

        if ($row && (int)$row['value'] !== $own_product_id) {
            throw new ApiError(409, 'SKU ' . $sku . ' maps to the same URL keyword as another product');
        }
    }

    private function productRepresentation(array $product): array {
        $id = (int)$product['product_id'];
        $model = $this->admin('catalog/product');
        $descriptions = $model->getDescriptions($id);
        $category_ids = $model->getCategories($id);
        $category_id = $category_ids ? (int)$category_ids[0] : null;

        $brand = '';
        if ((int)$product['manufacturer_id']) {
            $manufacturer = $this->admin('catalog/manufacturer')->getManufacturer((int)$product['manufacturer_id']);
            $brand = $this->decode((string)($manufacturer['name'] ?? ''));
        }

        $attributes = [];
        foreach ($model->getAttributes($id) as $attribute) {
            $info = $this->admin('catalog/attribute')->getDescription((int)$attribute['attribute_id'], $this->language_id);
            $text = $attribute['product_attribute_description'][$this->language_id]['text'] ?? '';
            $attributes[$this->decode((string)($info['name'] ?? $attribute['attribute_id']))] = $this->decode($text);
        }

        return [
            'id' => $id,
            'sku' => (string)$product['model'],
            'name' => $this->decode($descriptions[$this->language_id]['name'] ?? ''),
            'category_id' => $category_id,
            'category_slug' => $category_id ? $this->slugOfCategory($category_id) : null,
            'brand' => $brand,
            'description' => $this->decodeDescription((string)($descriptions[$this->language_id]['description'] ?? '')),
            'attributes' => (object)$attributes,
            'price' => (float)$product['price'],
            'quantity' => (int)$product['quantity'],
            'image_url' => $product['image'] ? HTTP_SERVER . 'image/' . $product['image'] : null,
        ];
    }

    // ---------------------------------------------------- brands and attributes

    private function ensureManufacturer(string $brand): int {
        $name = $this->encode($brand);
        $row = $this->db->query("SELECT `manufacturer_id` FROM `" . DB_PREFIX . "manufacturer` WHERE `name` = '" . $this->db->escape($name) . "' LIMIT 1")->row;
        if ($row) {
            return (int)$row['manufacturer_id'];
        }

        return $this->admin('catalog/manufacturer')->addManufacturer([
            'name' => $name,
            'image' => '',
            'sort_order' => 0,
            'manufacturer_store' => [0],
            'manufacturer_seo_url' => [0 => [$this->language_id => $this->keyword($brand, 'brand')]],
        ]);
    }

    private function ensureAttributeGroup(): int {
        $row = $this->db->query("SELECT `attribute_group_id` FROM `" . DB_PREFIX . "attribute_group_description` WHERE `name` = '" . $this->db->escape(self::ATTRIBUTE_GROUP) . "' AND `language_id` = '" . $this->language_id . "' LIMIT 1")->row;
        if ($row) {
            return (int)$row['attribute_group_id'];
        }

        return $this->admin('catalog/attribute_group')->addAttributeGroup([
            'sort_order' => 1,
            'attribute_group_description' => [$this->language_id => ['name' => self::ATTRIBUTE_GROUP]],
        ]);
    }

    /** @return array<int, array<string, mixed>> product_attribute data for the admin model */
    private function ensureAttributes(array $attributes): array {
        $result = [];
        $group_id = null;

        foreach ($attributes as $attribute_name => $value) {
            if ((string)$attribute_name === '' || !is_scalar($value)) {
                throw new ApiError(400, 'attributes must map names to text values');
            }
            $attribute_name = $this->encode((string)$attribute_name);

            $row = $this->db->query("SELECT `a`.`attribute_id` FROM `" . DB_PREFIX . "attribute` `a` LEFT JOIN `" . DB_PREFIX . "attribute_description` `ad` ON (`a`.`attribute_id` = `ad`.`attribute_id`) WHERE `ad`.`name` = '" . $this->db->escape($attribute_name) . "' AND `ad`.`language_id` = '" . $this->language_id . "' LIMIT 1")->row;

            if ($row) {
                $attribute_id = (int)$row['attribute_id'];
            } else {
                $group_id ??= $this->ensureAttributeGroup();
                $attribute_id = $this->admin('catalog/attribute')->addAttribute([
                    'attribute_group_id' => $group_id,
                    'sort_order' => 0,
                    'attribute_description' => [$this->language_id => ['name' => $attribute_name]],
                ]);
            }

            $result[] = [
                'attribute_id' => $attribute_id,
                'product_attribute_description' => [$this->language_id => ['text' => $this->encode((string)$value)]],
            ];
        }

        return $result;
    }

    // ----------------------------------------------------------------- helpers

    /** OpenCart prints stored text as HTML (its own forms store it encoded), so text from the API is encoded on write... */
    private function encode(string $text): string {
        return htmlspecialchars($text, ENT_QUOTES, 'UTF-8');
    }

    /** ...and decoded on read, so the API returns exactly what was sent. */
    private function decode(string $text): string {
        return html_entity_decode($text, ENT_QUOTES, 'UTF-8');
    }

    /**
     * The storefront decodes a description once and prints it as HTML (it is rich text in OpenCart), so plain text
     * becomes escaped HTML with line breaks first and is then encoded like every other stored text.
     */
    private function encodeDescription(string $text): string {
        return $this->encode(nl2br($this->encode($text)));
    }

    private function decodeDescription(string $stored): string {
        return trim($this->decode(strip_tags($this->decode($stored))));
    }

    private function assertSlug(string $slug): void {
        if (!preg_match('/^[a-z0-9-]+$/', $slug)) {
            throw new ApiError(400, 'slug must match ^[a-z0-9-]+$');
        }
    }

    private function body(): array {
        $raw = file_get_contents('php://input');
        $data = json_decode((string)$raw, true);

        if (!is_array($data)) {
            throw new ApiError(400, 'Body must be a JSON object');
        }

        return $data;
    }

    private function requiredString(array $body, string $field): string {
        if (!isset($body[$field]) || !is_string($body[$field]) || trim($body[$field]) === '') {
            throw new ApiError(400, $field . ' is required');
        }

        return trim($body[$field]);
    }

    private function respond(int $status, array $body): void {
        http_response_code($status);
        $this->response->addHeader('Content-Type: application/json; charset=utf-8');
        $this->response->setOutput(json_encode($body, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRESERVE_ZERO_FRACTION));
    }
}

class ApiError extends \RuntimeException {
    public function __construct(public readonly int $status, string $message) {
        parent::__construct($message);
    }
}
