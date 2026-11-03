// Generates data/sample-catalog.json and placeholder photos in data/images/.
// Run from the repository root:  dotnet run tools/generate-sample/generate.cs
// The output is deterministic, so regenerating produces identical files.
// All brands and products are invented; nothing is copied from a real retailer.

#:property PublishAot=false

using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;

var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
var imagesDirectory = Path.Combine(dataDirectory, "images");
Directory.CreateDirectory(imagesDirectory);

var categories = new[]
{
    new Category("electronics", "Электроника", null),
    new Category("smartphones", "Смартфоны", "electronics"),
    new Category("laptops", "Ноутбуки", "electronics"),
    new Category("headphones", "Наушники", "electronics"),
    new Category("tv", "Телевизоры", "electronics"),
};

var products = new List<Product>();

void Add(string category, string prefix, string brand, string name, string description,
         (string, string)[] attributes, int price, int quantity, int hue)
{
    var number = products.Count(p => p.Category == category) + 1;
    var sku = $"{prefix}-{number:000}";
    products.Add(new Product(sku, name, category, brand, description,
        attributes.ToDictionary(a => a.Item1, a => a.Item2), price, quantity, $"{sku.ToLowerInvariant()}.png", hue));
}

// Smartphones
(string, string)[] Phone(string memory, string ram, string screen, string battery, string camera) =>
    [("Память", memory), ("Оперативная память", ram), ("Диагональ экрана", screen), ("Аккумулятор", battery), ("Основная камера", camera)];

Add("smartphones", "PH", "Norda", "Смартфон Norda Pulse 5 128 ГБ", "Компактный смартфон для повседневных задач с ярким экраном и быстрой зарядкой.", Phone("128 ГБ", "6 ГБ", "6.1\"", "4300 мАч", "50 Мп"), 24990, 14, 10);
Add("smartphones", "PH", "Norda", "Смартфон Norda Pulse 5 Pro 256 ГБ", "Версия с увеличенной памятью и улучшенной камерой для съёмки в темноте.", Phone("256 ГБ", "8 ГБ", "6.4\"", "4700 мАч", "64 Мп"), 36990, 9, 12);
Add("smartphones", "PH", "Vektor", "Смартфон Vektor X20 128 ГБ", "Доступный смартфон с большим экраном и ёмким аккумулятором на два дня работы.", Phone("128 ГБ", "4 ГБ", "6.6\"", "5000 мАч", "48 Мп"), 15990, 30, 30);
Add("smartphones", "PH", "Vektor", "Смартфон Vektor X20 Plus 256 ГБ", "Расширенная версия с экраном 120 Гц и быстрой памятью.", Phone("256 ГБ", "8 ГБ", "6.7\"", "5000 мАч", "64 Мп"), 22990, 18, 32);
Add("smartphones", "PH", "Aurum", "Смартфон Aurum One 256 ГБ", "Флагман с металлическим корпусом, защитой от воды и отличной камерой.", Phone("256 ГБ", "12 ГБ", "6.8\"", "5200 мАч", "108 Мп"), 69990, 6, 45);
Add("smartphones", "PH", "Aurum", "Смартфон Aurum One Mini 128 ГБ", "Небольшой флагман, удобный в одной руке.", Phone("128 ГБ", "8 ГБ", "5.8\"", "3900 мАч", "50 Мп"), 54990, 0, 47);
Add("smartphones", "PH", "Lumen", "Смартфон Lumen Spark 64 ГБ", "Бюджетная модель для звонков, мессенджеров и видео.", Phone("64 ГБ", "3 ГБ", "6.5\"", "5000 мАч", "13 Мп"), 8990, 42, 55);
Add("smartphones", "PH", "Lumen", "Смартфон Lumen Spark Max 128 ГБ", "Бюджетный смартфон с увеличенной памятью и двойной камерой.", Phone("128 ГБ", "4 ГБ", "6.6\"", "5000 мАч", "50 Мп"), 11990, 25, 57);

// Laptops
(string, string)[] Laptop(string cpu, string ram, string disk, string screen, string weight) =>
    [("Процессор", cpu), ("Оперативная память", ram), ("Накопитель", disk), ("Диагональ экрана", screen), ("Вес", weight)];

Add("laptops", "LT", "Vektor", "Ноутбук Vektor Book 14", "Лёгкий ноутбук для учёбы и работы с документами.", Laptop("4 ядра, 2.8 ГГц", "8 ГБ", "SSD 256 ГБ", "14\"", "1.4 кг"), 42990, 12, 200);
Add("laptops", "LT", "Vektor", "Ноутбук Vektor Book 15 Pro", "Универсальный ноутбук с экраном IPS и быстрым SSD.", Laptop("6 ядер, 3.2 ГГц", "16 ГБ", "SSD 512 ГБ", "15.6\"", "1.8 кг"), 64990, 8, 202);
Add("laptops", "LT", "Norda", "Ноутбук Norda Air 13", "Ультрабук с аккумулятором на 15 часов и тонким корпусом.", Laptop("8 ядер, 3.0 ГГц", "16 ГБ", "SSD 512 ГБ", "13.3\"", "1.1 кг"), 89990, 5, 210);
Add("laptops", "LT", "Norda", "Ноутбук Norda Air 15", "Ультрабук с большим экраном для работы и творчества.", Laptop("8 ядер, 3.0 ГГц", "16 ГБ", "SSD 1 ТБ", "15.3\"", "1.5 кг"), 109990, 3, 212);
Add("laptops", "LT", "Kobalt", "Игровой ноутбук Kobalt Storm 16", "Мощный игровой ноутбук с дискретной графикой и экраном 165 Гц.", Laptop("8 ядер, 4.2 ГГц", "32 ГБ", "SSD 1 ТБ", "16\"", "2.4 кг"), 149990, 4, 220);
Add("laptops", "LT", "Kobalt", "Игровой ноутбук Kobalt Storm 15", "Игровой ноутбук среднего уровня для современных игр.", Laptop("6 ядер, 4.0 ГГц", "16 ГБ", "SSD 512 ГБ", "15.6\"", "2.2 кг"), 99990, 0, 222);
Add("laptops", "LT", "Lumen", "Ноутбук Lumen Go 15", "Недорогой ноутбук для интернета и офисных программ.", Laptop("4 ядра, 2.4 ГГц", "8 ГБ", "SSD 256 ГБ", "15.6\"", "1.9 кг"), 34990, 20, 230);

// Headphones
(string, string)[] Headphones(string type, string connection, string battery, string noise, string weight) =>
    [("Тип", type), ("Подключение", connection), ("Время работы", battery), ("Шумоподавление", noise), ("Вес", weight)];

Add("headphones", "HP", "Aurum", "Беспроводные наушники Aurum Buds Pro", "Вкладыши с активным шумоподавлением и чистым звуком.", Headphones("Вкладыши", "Bluetooth 5.3", "8 ч (30 ч с кейсом)", "Активное", "5 г"), 12990, 35, 100);
Add("headphones", "HP", "Aurum", "Беспроводные наушники Aurum Over 2", "Полноразмерные наушники для долгих прослушиваний.", Headphones("Накладные", "Bluetooth 5.3", "40 ч", "Активное", "260 г"), 24990, 11, 102);
Add("headphones", "HP", "Norda", "Беспроводные наушники Norda Tune", "Лёгкие вкладыши для спорта и прогулок.", Headphones("Вкладыши", "Bluetooth 5.2", "6 ч (24 ч с кейсом)", "Нет", "4 г"), 4990, 60, 110);
Add("headphones", "HP", "Norda", "Наушники Norda Studio", "Проводные мониторные наушники для работы со звуком.", Headphones("Мониторные", "Кабель 3.5 мм", "—", "Пассивное", "320 г"), 8990, 15, 112);
Add("headphones", "HP", "Kobalt", "Игровая гарнитура Kobalt Echo", "Гарнитура со съёмным микрофоном и объёмным звуком.", Headphones("Полноразмерные", "USB", "—", "Пассивное", "350 г"), 6990, 22, 120);
Add("headphones", "HP", "Lumen", "Беспроводные наушники Lumen Pods", "Недорогие вкладыши с зарядным кейсом.", Headphones("Вкладыши", "Bluetooth 5.1", "5 ч (20 ч с кейсом)", "Нет", "4 г"), 2490, 80, 130);
Add("headphones", "HP", "Lumen", "Наушники Lumen Wired", "Простые проводные наушники-вкладыши.", Headphones("Вкладыши", "Кабель 3.5 мм", "—", "Пассивное", "12 г"), 790, 150, 132);

// TVs
(string, string)[] Tv(string diagonal, string resolution, string matrix, string smart, string hdr) =>
    [("Диагональ", diagonal), ("Разрешение", resolution), ("Матрица", matrix), ("Smart TV", smart), ("HDR", hdr)];

Add("tv", "TV", "Vektor", "Телевизор Vektor 43\" 4K", "Телевизор 4K с онлайн-кинотеатрами и голосовым пультом.", Tv("43\"", "3840x2160", "LED", "Да", "HDR10"), 27990, 10, 260);
Add("tv", "TV", "Vektor", "Телевизор Vektor 55\" 4K", "Большой экран 4K для просмотра фильмов и игр.", Tv("55\"", "3840x2160", "LED", "Да", "HDR10"), 38990, 7, 262);
Add("tv", "TV", "Aurum", "Телевизор Aurum 55\" OLED", "OLED-экран с глубоким чёрным цветом и частотой 120 Гц.", Tv("55\"", "3840x2160", "OLED", "Да", "HDR10+, Dolby Vision"), 109990, 2, 270);
Add("tv", "TV", "Aurum", "Телевизор Aurum 65\" OLED", "Телевизор для домашнего кинотеатра с поддержкой Dolby Atmos.", Tv("65\"", "3840x2160", "OLED", "Да", "HDR10+, Dolby Vision"), 159990, 0, 272);
Add("tv", "TV", "Lumen", "Телевизор Lumen 32\" HD", "Небольшой телевизор для кухни или детской.", Tv("32\"", "1366x768", "LED", "Да", "Нет"), 11990, 28, 280);
Add("tv", "TV", "Lumen", "Телевизор Lumen 50\" 4K", "Недорогой телевизор 4K со Smart TV.", Tv("50\"", "3840x2160", "LED", "Да", "HDR10"), 25990, 13, 282);

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

var catalog = new
{
    generated = true,
    source = "Generated by tools/generate-sample/generate.cs. All brands and products are invented; nothing is copied from a real retailer.",
    categories = categories.Select(c => c.Parent is null
        ? (object)new { slug = c.Slug, name = c.Name }
        : new { slug = c.Slug, name = c.Name, parent = c.Parent }),
    products = products.Select(p => new
    {
        sku = p.Sku, name = p.Name, category = p.Category, brand = p.Brand, description = p.Description,
        attributes = p.Attributes, price = p.Price, quantity = p.Quantity, image = p.Image,
    }),
};

File.WriteAllText(Path.Combine(dataDirectory, "sample-catalog.json"), JsonSerializer.Serialize(catalog, options) + "\n");

foreach (var product in products)
{
    File.WriteAllBytes(Path.Combine(imagesDirectory, product.Image), Placeholder.Png(product.Hue, product.Category));
}

Console.WriteLine($"Wrote {products.Count} products in {categories.Length} categories to {dataDirectory}");

record Category(string Slug, string Name, string? Parent);

record Product(string Sku, string Name, string Category, string Brand, string Description,
    Dictionary<string, string> Attributes, int Price, int Quantity, string Image, int Hue);

/// <summary>Draws a simple coloured placeholder picture (a device silhouette on a gradient) as a PNG.</summary>
static class Placeholder
{
    const int Size = 600;

    public static byte[] Png(int hue, string category)
    {
        var pixels = new byte[Size * Size * 3];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b) = Hsv(hue, 0.25, 0.97 - 0.18 * y / Size);
                if (InsideShape(category, x, y))
                {
                    (r, g, b) = Hsv(hue, 0.65, 0.35);
                    if (InsideScreen(category, x, y))
                    {
                        (r, g, b) = Hsv(hue, 0.35, 0.92);
                    }
                }
                var i = (y * Size + x) * 3;
                pixels[i] = r; pixels[i + 1] = g; pixels[i + 2] = b;
            }
        }
        return Encode(pixels);
    }

    static bool InsideShape(string category, int x, int y) => category switch
    {
        "smartphones" => Rounded(x, y, 220, 90, 380, 510, 28),
        "laptops" => Rounded(x, y, 110, 170, 490, 400, 14) || Rounded(x, y, 60, 400, 540, 430, 10),
        "headphones" => Ring(x, y, 300, 310, 150, 200) && y < 330 || Rounded(x, y, 130, 300, 190, 440, 24) || Rounded(x, y, 410, 300, 470, 440, 24),
        _ => Rounded(x, y, 70, 150, 530, 420, 12) || Rounded(x, y, 250, 420, 350, 470, 6),
    };

    static bool InsideScreen(string category, int x, int y) => category switch
    {
        "smartphones" => Rounded(x, y, 232, 120, 368, 480, 14),
        "laptops" => Rounded(x, y, 126, 186, 474, 384, 6),
        "tv" => Rounded(x, y, 84, 164, 516, 406, 6),
        _ => false,
    };

    static bool Rounded(int x, int y, int x0, int y0, int x1, int y1, int radius)
    {
        if (x < x0 || x > x1 || y < y0 || y > y1) { return false; }
        var cx = Math.Clamp(x, x0 + radius, x1 - radius);
        var cy = Math.Clamp(y, y0 + radius, y1 - radius);
        return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
    }

    static bool Ring(int x, int y, int cx, int cy, int inner, int outer)
    {
        var d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
        return d >= inner * inner && d <= outer * outer;
    }

    static (byte, byte, byte) Hsv(double h, double s, double v)
    {
        h = h % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x), 3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    static byte[] Encode(byte[] rgb)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < Size; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgb, y * Size * 3, Size * 3);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", [.. Int(Size), .. Int(Size), 8, 2, 0, 0, 0]);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    static byte[] Int(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    static void Chunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(Int(data.Length));
        stream.Write(typeBytes);
        stream.Write(data);
        stream.Write(Int(unchecked((int)Crc32([.. typeBytes, .. data]))));
    }

    static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }
        return ~crc;
    }
}
