// Builds a real Russian electronics catalog for the demo shop from public Wildberries data:
// the Hugging Face dump nyuuzyou/wb-products (CC0) as the index, and Wildberries' public card files and photos for
// details. Prices and stock are generated. The output is a product file (specs/001-test-shop) plus images/.
// Run from the repository root:
//   dotnet run tools/build-catalog/build-catalog.cs -- [--limit N] [--dumps 12] [--out out/catalog]
//                                                      [--cache .cache/build-catalog] [--archive]
// See specs/003-real-demo-catalog/contracts/build-catalog-cli.md. Cards and photos are cached, so a re-run makes no
// requests for products it already has, and the output is deterministic.

#:property PublishAot=false
#:package ZstdSharp.Port@0.8.8
#:package SkiaSharp@4.157.0
#:package JsonSchema.Net@9.7.0

using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using SkiaSharp;
using ZstdSharp;

Options options;
try
{
    options = Options.Parse(args);
}
catch (ArgumentException e)
{
    Console.Error.WriteLine($"error: {e.Message}");
    Console.Error.WriteLine("usage: build-catalog.cs [--limit N] [--dumps LIST] [--out DIR] [--cache DIR] [--archive]");
    return 2;
}

var cache = new Cache(options.Cache);
using var wildberries = new Wildberries(cache);

// 1. Candidates from the dump: one per model (imt_id), lowest nm_id wins.
List<Candidate> candidates;
try
{
    candidates = await Dump.ReadCandidatesAsync(options, cache);
}
catch (Exception e) when (e is HttpRequestException or IOException or ZstdException or JsonException)
{
    Console.Error.WriteLine($"error: could not read the dump: {e.Message}");
    return 3;
}
Console.WriteLine($"Candidates: {candidates.Count} models in {candidates.Select(c => c.Subject).Distinct().Count()} subjects");

// 2. Select: fetch cards in a spread order, filter noise, stop at the limit.
var skipped = new SortedDictionary<string, int>();
var selected = new List<Product>();
var queues = Subjects.All.ToDictionary(
    s => s.Name,
    s => candidates.Where(c => c.Subject == s.Name).OrderBy(c => Spread(c.NmId)).ThenBy(c => c.NmId).ToList());
var models = new HashSet<string>();
// With --limit, a brand gets at most a tenth of a subject's share (at least 3): 3 for 100 products, 38 for 3000.
var maxPerBrand = Math.Max(3, (int)Math.Ceiling((options.Limit ?? 0) / (double)Subjects.All.Length / 10));
var brandsTaken = Subjects.All.ToDictionary(s => s.Name, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

while (queues.Values.Any(q => q.Count > 0) && (options.Limit is null || selected.Count < options.Limit))
{
    foreach (var subject in Subjects.All)
    {
        if (options.Limit is not null && selected.Count >= options.Limit)
        {
            break;
        }
        var queue = queues[subject.Name];
        while (queue.Count > 0)
        {
            // With a limit, prefer a brand this subject does not have yet (the dump's brand is a cheap hint).
            var index = options.Limit is null ? 0 : Math.Max(0, queue.FindIndex(c => !brandsTaken[subject.Name].Contains(c.DumpBrand)));
            var candidate = queue[index];
            queue.RemoveAt(index);
            var (product, reason) = await Mapper.TryBuildAsync(candidate, wildberries);
            if (product is not null && !models.Add(ModelKey(product.Name)))
            {
                (product, reason) = (null, "same model");
            }
            if (product is not null && options.Limit is not null &&
                selected.Count(p => p.Category == product.Category && string.Equals(p.Brand, product.Brand, StringComparison.OrdinalIgnoreCase)) >= maxPerBrand)
            {
                (product, reason) = (null, "brand quota");
            }
            if (product is null)
            {
                skipped[reason!] = skipped.GetValueOrDefault(reason!) + 1;
                continue;
            }
            selected.Add(product);
            brandsTaken[subject.Name].Add(product.Brand);
            brandsTaken[subject.Name].Add(candidate.DumpBrand);
            if (selected.Count % 25 == 0)
            {
                Console.WriteLine($"  {selected.Count} products selected ({wildberries.Requests} requests so far)");
            }
            break;
        }
    }
}

// 3. Assemble, validate, write.
var products = selected.OrderBy(p => p.Category, StringComparer.Ordinal).ThenBy(p => p.Sku, StringComparer.Ordinal).ToList();
var categories = new List<Category>();
foreach (var subject in Subjects.All.Where(s => products.Any(p => p.Category == s.Slug)))
{
    if (!categories.Any(c => c.Slug == subject.RootSlug))
    {
        categories.Add(new Category(subject.RootSlug, subject.Root, null));
    }
    categories.Add(new Category(subject.Slug, subject.Name, subject.RootSlug));
}
categories = [.. categories.Where(c => c.Parent is null).OrderBy(c => c.Slug), .. categories.Where(c => c.Parent is not null).OrderBy(c => c.Slug)];

var document = Output.Build(categories, products);
var problems = Output.Check(document, categories, products, options.Limit);
if (problems.Count > 0)
{
    Console.Error.WriteLine("error: the catalog is not valid, nothing was written:");
    problems.ForEach(p => Console.Error.WriteLine($"  - {p}"));
    PrintSkipped();
    return 1;
}

var images = Path.Combine(options.Out, "images");
if (Directory.Exists(images))
{
    Directory.Delete(images, recursive: true);
}
Directory.CreateDirectory(images);
foreach (var product in products)
{
    Photos.WriteJpeg(cache.PhotoPath(product.NmId), Path.Combine(images, product.Image));
}
var catalogPath = Path.Combine(options.Out, "catalog.json");
File.WriteAllText(catalogPath, document.ToJsonString(Output.Json) + "\n");
if (options.Archive)
{
    Output.Archive(options.Out);
}

// 4. Summary.
var size = new FileInfo(catalogPath).Length + Directory.GetFiles(images).Sum(f => new FileInfo(f).Length);
Console.WriteLine($"Wrote {products.Count} products to {catalogPath} ({size / 1024.0 / 1024.0:F1} MB with images)");
Console.WriteLine($"Subjects: {string.Join(", ", products.GroupBy(p => p.Category).Select(g => $"{g.Key} {g.Count()}"))}");
Console.WriteLine($"Brands: {products.Select(p => p.Brand).Distinct(StringComparer.OrdinalIgnoreCase).Count()}; out of stock: {products.Count(p => p.Quantity == 0)}");
PrintSkipped();
Console.WriteLine($"Wildberries requests: {wildberries.Requests} over the network, {wildberries.CacheHits} from cache");
return 0;

void PrintSkipped() =>
    Console.WriteLine($"Skipped: {(skipped.Count == 0 ? "none" : string.Join(", ", skipped.Select(s => $"{s.Key} {s.Value}")))}");

// Sellers list colour and memory variants as separate models ("MacBook Air 15/M2/8/256 (Silver)"): compare names up
// to the first "/" or "(".
static string ModelKey(string name) => Regex.Replace(name.Split('/', '(')[0], @"\s+", " ").Trim().ToLowerInvariant();

// A stable spread of product IDs (string.GetHashCode is randomized per process).
static uint Spread(long nmId) => unchecked((uint)nmId * 2654435761u);

record Options(int? Limit, int[] Dumps, string Out, string Cache, bool Archive)
{
    public static Options Parse(string[] args)
    {
        int? limit = null;
        var dumps = new[] { 12 };
        var output = Path.Combine("out", "catalog");
        var cache = Path.Combine(".cache", "build-catalog");
        var archive = false;
        for (var i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--limit":
                    limit = int.TryParse(Value(), out var n) && n > 0 ? n : throw new ArgumentException("--limit must be a positive number");
                    break;
                case "--dumps":
                    dumps = ParseDumps(Value());
                    break;
                case "--out":
                    output = Value();
                    break;
                case "--cache":
                    cache = Value();
                    break;
                case "--archive":
                    archive = true;
                    break;
                default:
                    throw new ArgumentException($"unknown option {args[i]}");
            }
        }
        return new Options(limit, dumps, output, cache, archive);
    }

    // "12", "9-12" or "1,3,9-12"; dump files are basket-01 … basket-12.
    static int[] ParseDumps(string text)
    {
        var result = new SortedSet<int>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var bounds = part.Split('-');
            if (bounds.Length > 2 || !bounds.All(b => int.TryParse(b, out var v) && v is >= 1 and <= 12))
            {
                throw new ArgumentException($"--dumps: '{part}' is not a number or range between 1 and 12");
            }
            var (from, to) = (int.Parse(bounds[0]), int.Parse(bounds[^1]));
            for (var d = Math.Min(from, to); d <= Math.Max(from, to); d++)
            {
                result.Add(d);
            }
        }
        return result.Count > 0 ? [.. result.Reverse()] : throw new ArgumentException("--dumps is empty");
    }
}

/// <summary>A catalog category of the shop: a Wildberries subject under a fixed root, with price range and checks.</summary>
record Subject(string Name, string Root, string[] Aliases, int MinPrice, int MaxPrice, string[] Required)
{
    public string Slug => Slugs.From(Name);
    public string RootSlug => Slugs.From(Root);

    /// <summary>Every pattern must match some characteristic name, so accessories filed here are dropped.</summary>
    public bool HasKeyCharacteristics(IReadOnlyCollection<string> names) =>
        Required.All(pattern => names.Any(n => Regex.IsMatch(n, pattern, RegexOptions.IgnoreCase)));
}

static class Subjects
{
    // Key characteristics were taken from real cards (2026-11-21); prices are plausible demo ranges in rubles.
    public static readonly Subject[] All =
    [
        new("Смартфоны", "Смартфоны и аксессуары", [], 8_000, 150_000, ["^Диагональ", "памят|Процессор|SIM", "аккумулятор"]),
        new("Ноутбуки", "Ноутбуки и компьютеры", [], 30_000, 250_000, ["^Процессор|Линейка процессоров", "оперативной памяти", "Разрешение экрана|^Диагональ"]),
        new("Планшеты", "Смартфоны и аксессуары", [], 10_000, 120_000, ["^Диагональ", "памят", "Операционная система|Версия операционной системы|Процессор"]),
        new("Телевизоры", "Телевизоры и аудиотехника", [], 15_000, 200_000, ["^Диагональ", "Разрешение экрана", "HDMI|Операционная система|Smart"]),
        new("Мониторы", "Периферия и аксессуары", [], 10_000, 100_000, ["^Диагональ", "Разрешение экрана"]),
        new("Наушники", "Телевизоры и аудиотехника", ["Наушники беспроводные"], 1_000, 30_000, ["Вид наушников|Тип наушников|Тип крепления наушников", "частота|Тип подключения"]),
        new("Колонки", "Телевизоры и аудиотехника", ["Умные колонки", "Портативные колонки"], 1_500, 40_000, ["частота|Мощность|Акустический тип|Вид колонки", "Питание|Беспроводные интерфейсы|Тип подключения"]),
        new("Смарт-часы", "Смартфоны и аксессуары", [], 3_000, 60_000, ["^Диагональ|Тип дисплея|Циферблат", "Совместимые ОС|Беспроводные интерфейсы|Bluetooth"]),
    ];

    public static Subject? Find(string? name) =>
        All.FirstOrDefault(s => s.Name == name || s.Aliases.Contains(name));
}

static class Slugs
{
    static readonly Dictionary<char, string> Letters = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo", ['ж'] = "zh",
        ['з'] = "z", ['и'] = "i", ['й'] = "j", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shh", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya",
    };

    public static string From(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            text.Append(Letters.TryGetValue(c, out var latin) ? latin : char.IsAsciiLetterOrDigit(c) ? c.ToString() : "-");
        }
        return Regex.Replace(text.ToString(), "-+", "-").Trim('-');
    }
}

record Candidate(long NmId, long ImtId, string Subject, string DumpBrand);

static class Dump
{
    const string Url = "https://huggingface.co/datasets/nyuuzyou/wb-products/resolve/main/basket-{0:D2}.json.zst";

    /// <summary>Streams the dump files (nothing is kept but the filtered candidates, cached per file).</summary>
    public static async Task<List<Candidate>> ReadCandidatesAsync(Options options, Cache cache)
    {
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Wildberries.UserAgent);
        var byModel = new Dictionary<long, Candidate>();
        foreach (var number in options.Dumps)
        {
            foreach (var candidate in await ReadFileAsync(http, number, cache))
            {
                if (!byModel.TryGetValue(candidate.ImtId, out var existing) || candidate.NmId < existing.NmId)
                {
                    byModel[candidate.ImtId] = candidate;
                }
            }
            // With a limit, a generous pool is enough; the smallest dump alone gives several hundred models.
            if (options.Limit is { } limit && byModel.Count >= limit * 10)
            {
                break;
            }
        }
        return [.. byModel.Values.OrderBy(c => c.NmId)];
    }

    static async Task<List<Candidate>> ReadFileAsync(HttpClient http, int number, Cache cache)
    {
        var cached = cache.CandidatesPath(number);
        if (File.Exists(cached))
        {
            return JsonSerializer.Deserialize<List<Candidate>>(File.ReadAllText(cached))!;
        }

        Console.WriteLine($"Reading dump basket-{number:D2} …");
        var result = new List<Candidate>();
        using var response = await http.GetAsync(string.Format(Url, number), HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        await using var zstd = new DecompressionStream(body);
        using var reader = new StreamReader(zstd, Encoding.UTF8);
        var lines = 0L;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (++lines % 2_000_000 == 0)
            {
                Console.WriteLine($"  {lines / 1_000_000}M lines, {result.Count} candidates");
            }
            // Cheap pre-check before parsing: most lines are clothes, not electronics.
            if (!Subjects.All.Any(s => line.Contains(s.Name, StringComparison.Ordinal) || s.Aliases.Any(a => line.Contains(a, StringComparison.Ordinal))))
            {
                continue;
            }
            using var record = JsonDocument.Parse(line);
            var root = record.RootElement;
            var subject = Subjects.Find(Text(root, "subj_name"));
            if (subject is null || !root.TryGetProperty("nm_id", out var nm) || !root.TryGetProperty("imt_id", out var imt))
            {
                continue;
            }
            result.Add(new Candidate(nm.GetInt64(), imt.GetInt64(), subject.Name, Text(root, "brand_name")?.Trim() ?? ""));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        File.WriteAllText(cached, JsonSerializer.Serialize(result));
        Console.WriteLine($"  basket-{number:D2}: {lines} lines, {result.Count} candidates");
        return result;
    }

    static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

class Cache(string root)
{
    public string CardPath(long nmId) => Path.Combine(root, "cards", $"{nmId}.json");
    public string PhotoPath(long nmId) => Path.Combine(root, "photos", $"{nmId}.webp");
    public string MissingPath(long nmId, string what) => Path.Combine(root, "missing", $"{nmId}.{what}");
    public string HostsPath => Path.Combine(root, "hosts.json");
    public string CandidatesPath(int dump) => Path.Combine(root, "candidates", $"basket-{dump:D2}.json");

    public static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }
}

/// <summary>Wildberries' static card files and photos, at one request per second, cached on disk.</summary>
sealed class Wildberries(Cache cache) : IDisposable
{
    public const string UserAgent = "internet-shop-manager-ai-assistant demo catalog builder (non-commercial; 1 req/s)";
    const int MaxHost = 40;
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    readonly HttpClient http = CreateClient();
    readonly SortedDictionary<long, int> hosts = LoadHosts(cache.HostsPath);
    DateTime nextRequest = DateTime.MinValue;

    public int Requests { get; private set; }
    public int CacheHits { get; private set; }

    /// <summary>The card JSON, or null when the product no longer exists (or failed after retries).</summary>
    public async Task<JsonDocument?> CardAsync(long nmId)
    {
        var path = cache.CardPath(nmId);
        if (File.Exists(path))
        {
            CacheHits++;
            return JsonDocument.Parse(File.ReadAllBytes(path));
        }
        if (File.Exists(cache.MissingPath(nmId, "card")))
        {
            CacheHits++;
            return null;
        }

        var vol = nmId / 100_000;
        foreach (var host in HostsToTry(vol))
        {
            var (status, content) = await GetAsync($"{Base(host, nmId)}/info/ru/card.json");
            if (status == HttpStatusCode.OK)
            {
                if (!hosts.ContainsKey(vol))
                {
                    hosts[vol] = host;
                    Cache.Write(cache.HostsPath, JsonSerializer.SerializeToUtf8Bytes(hosts));
                }
                Cache.Write(path, content!);
                return JsonDocument.Parse(content!);
            }
            if (status != HttpStatusCode.NotFound)
            {
                return null; // a persistent failure: skip now, try again on the next run
            }
            if (hosts.ContainsKey(vol))
            {
                break; // the right host says the card is gone
            }
        }
        Cache.Write(cache.MissingPath(nmId, "card"), []);
        return null;
    }

    /// <summary>The main photo (WebP) path in the cache, or null when there is none.</summary>
    public async Task<string?> PhotoAsync(long nmId)
    {
        var path = cache.PhotoPath(nmId);
        if (File.Exists(path))
        {
            CacheHits++;
            return path;
        }
        if (File.Exists(cache.MissingPath(nmId, "photo")))
        {
            CacheHits++;
            return null;
        }
        if (!hosts.TryGetValue(nmId / 100_000, out var host))
        {
            return null;
        }
        var (status, content) = await GetAsync($"{Base(host, nmId)}/images/big/1.webp");
        if (status == HttpStatusCode.OK)
        {
            Cache.Write(path, content!);
            return path;
        }
        if (status == HttpStatusCode.NotFound)
        {
            Cache.Write(cache.MissingPath(nmId, "photo"), []);
        }
        return null;
    }

    static string Base(int host, long nmId) =>
        $"https://basket-{host:D2}.wbbasket.ru/vol{nmId / 100_000}/part{nmId / 1000}/{nmId}";

    // The known host for this vol; otherwise hosts of the nearest known vols first, then the rest.
    IEnumerable<int> HostsToTry(long vol)
    {
        if (hosts.TryGetValue(vol, out var known))
        {
            return [known];
        }
        var nearest = hosts.OrderBy(h => Math.Abs(h.Key - vol)).Select(h => h.Value);
        return nearest.Concat(Enumerable.Range(1, MaxHost)).Distinct();
    }

    async Task<(HttpStatusCode Status, byte[]? Content)> GetAsync(string url)
    {
        for (var attempt = 1; ; attempt++)
        {
            var wait = nextRequest - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait);
            }
            nextRequest = DateTime.UtcNow + Interval;
            Requests++;
            try
            {
                using var response = await http.GetAsync(url);
                var status = response.StatusCode;
                if (status == HttpStatusCode.OK)
                {
                    return (status, await response.Content.ReadAsByteArrayAsync());
                }
                if (attempt >= 3 || (status != HttpStatusCode.TooManyRequests && (int)status < 500))
                {
                    return (status, null);
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                if (attempt >= 3)
                {
                    Console.Error.WriteLine($"  giving up on {url}: {e.Message}");
                    return (HttpStatusCode.ServiceUnavailable, null);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
        }
    }

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    static SortedDictionary<long, int> LoadHosts(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<SortedDictionary<long, int>>(File.ReadAllText(path))! : [];

    public void Dispose() => http.Dispose();
}

record Category(string Slug, string Name, string? Parent);

record Product(long NmId, string Sku, string Name, string Category, string Brand, string Description,
               List<KeyValuePair<string, string>> Attributes, int Price, int Quantity, string Image);

static class Mapper
{
    // OpenCart columns: product name 255, brand and attribute names 64 characters, after HTML encoding.
    const int MaxName = 255;
    const int MaxShortName = 64;
    const int MinCharacteristics = 8;

    /// <summary>The product for a candidate, or null and the reason it was skipped.</summary>
    public static async Task<(Product? Product, string? Reason)> TryBuildAsync(Candidate candidate, Wildberries wildberries)
    {
        using var card = await wildberries.CardAsync(candidate.NmId);
        if (card is null)
        {
            return (null, "no card");
        }
        var root = card.RootElement;
        var subject = Subjects.Find(Text(root, "subj_name"));
        if (subject is null)
        {
            return (null, "subject changed");
        }
        var name = Clean(Text(root, "imt_name"));
        var brand = Brands.Normalize(Clean(Text(root, "selling", "brand_name")), name)
                    ?? Brands.Normalize(Clean(candidate.DumpBrand), name);
        if (brand is null || Encoded(brand) > MaxShortName)
        {
            return (null, "no brand");
        }
        var description = Clean(Text(root, "description"));
        if (name.Length == 0 || description.Length == 0)
        {
            return (null, "no name or description");
        }
        var attributes = Characteristics(root);
        if (attributes.Count < MinCharacteristics || !subject.HasKeyCharacteristics([.. attributes.Select(a => a.Key)]))
        {
            return (null, "not a " + subject.Slug);
        }
        if (await wildberries.PhotoAsync(candidate.NmId) is null)
        {
            return (null, "no photo");
        }

        var random = new Random((int)(candidate.NmId % int.MaxValue));
        // Log-uniform, so cheap products are more common than expensive ones, as in a real shop.
        var price = (int)Math.Round(Math.Exp(double.Lerp(Math.Log(subject.MinPrice), Math.Log(subject.MaxPrice), random.NextDouble())) / 1000) * 1000 - 10;
        var quantity = random.Next(100) < 5 ? 0 : random.Next(1, 51);
        return (new Product(candidate.NmId, $"WB-{candidate.NmId}", Cut(name, MaxName), subject.Slug, brand,
                            description, attributes, price, quantity, $"wb-{candidate.NmId}.jpg"), null);
    }

    // All characteristics, in card order; a name used in several groups becomes "<group>: <name>".
    static List<KeyValuePair<string, string>> Characteristics(JsonElement card)
    {
        var options = new List<(string Group, string Name, string Value)>();
        if (card.TryGetProperty("grouped_options", out var groups) && groups.ValueKind == JsonValueKind.Array && groups.GetArrayLength() > 0)
        {
            foreach (var group in groups.EnumerateArray())
            {
                var groupName = Clean(Text(group, "group_name"));
                if (group.TryGetProperty("options", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    options.AddRange(list.EnumerateArray().Select(o => (groupName, Clean(Text(o, "name")), Clean(Text(o, "value")))));
                }
            }
        }
        else if (card.TryGetProperty("options", out var flat) && flat.ValueKind == JsonValueKind.Array)
        {
            options.AddRange(flat.EnumerateArray().Select(o => ("", Clean(Text(o, "name")), Clean(Text(o, "value")))));
        }

        var repeated = options.GroupBy(o => o.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var result = new List<KeyValuePair<string, string>>();
        foreach (var (group, name, value) in options.Where(o => o.Name.Length > 0 && o.Value.Length > 0))
        {
            var key = Cut(repeated.Contains(name) && group.Length > 0 ? $"{group}: {name}" : name, MaxShortName);
            if (!result.Any(r => r.Key == key))
            {
                result.Add(new(key, value));
            }
        }
        return result;
    }

    static string? Text(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
            {
                return null;
            }
        }
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    static string Clean(string? text) => Regex.Replace(text ?? "", @"[ \t ]+", " ").Trim();

    static int Encoded(string text) => WebUtility.HtmlEncode(text).Length;

    // Cut so the HTML-encoded text fits the shop's column.
    static string Cut(string text, int max)
    {
        while (Encoded(text) > max)
        {
            text = text[..^1].TrimEnd();
        }
        return text;
    }
}

/// <summary>
/// Marketplace brands are what sellers typed: store names, "GALAXY" for Samsung, "не указан". Known manufacturers are
/// written the usual way; an unknown brand gives way to a manufacturer named in the product name (but not after
/// "для"/"на", as in "чехол для iPhone"); a brand that is a product or shop word is no brand.
/// </summary>
static class Brands
{
    static readonly string[] Known =
    [
        "Apple", "Samsung", "Xiaomi", "Huawei", "Honor", "Realme", "OPPO", "vivo", "Tecno", "Infinix", "Nokia", "Motorola",
        "OnePlus", "Google", "Sony", "LG", "TCL", "Hisense", "Haier", "Philips", "JBL", "Marshall", "Bang & Olufsen",
        "Harman Kardon", "Яндекс", "VK", "Lenovo", "Acer", "ASUS", "HP", "Dell", "MSI", "Gigabyte", "AOC", "BenQ",
        "ViewSonic", "Garmin", "Amazfit", "Fitbit", "Jabra", "Sennheiser", "Beats", "Edifier", "Defender", "SVEN", "Digma",
        "Prestigio", "Irbis", "Blackview", "Doogee", "Ulefone", "Oukitel", "Cubot", "Meizu", "ZTE", "Nothing", "Hyundai",
        "Thomson", "BBK", "Polarline", "Starwind", "Renova", "KIVI", "Toshiba", "Panasonic", "Sharp", "Razer", "Logitech",
        "HyperX", "SteelSeries", "Redmi", "POCO", "Borofone", "Hoco", "Baseus", "Anker", "Soundcore", "Mibro", "Aimoto",
        "Elari", "Haylou", "QCY", "Machenike", "Ninkear", "Chuwi", "Teclast", "Alldocube", "Jumper", "Mediacom",
        "Sber", "Hiper", "Leff", "Skyworth",
    ];

    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Galaxy"] = "Samsung", ["Samsung Galaxy"] = "Samsung", ["Mi"] = "Xiaomi", ["Xiaomi Mi"] = "Xiaomi",
        ["Yandex"] = "Яндекс", ["маршал"] = "Marshall", ["bang olufsen"] = "Bang & Olufsen", ["Xiaom"] = "Xiaomi", ["Яндекс Станция"] = "Яндекс", ["Huawei Honor"] = "Honor", ["Asus ROG"] = "ASUS",
    };

    static readonly string[] NotBrands =
    [
        "не указан", "нет бренда", "без бренда", "noname", "no name", "no brand", "нет", "tws", "smart watch", "smart",
    ];

    static readonly Regex ProductWord = new("наушник|колонк|смартфон|телефон|часы|телевизор|планшет|ноутбук|монитор|акустик|гаджет|" +
                                            "магазин|менеджер|мелочи|техник|shop|store|group|market|trade|smart ?watch",
        RegexOptions.IgnoreCase);

    public static string? Normalize(string brand, string name)
    {
        var known = Canonical(brand);
        if (known is not null)
        {
            return known;
        }
        var fromName = Known.Concat(Aliases.Keys)
            .Select(k => (Key: k, Match: Regex.Match(name, $@"(?<![\p{{L}}\d])(?<!(для|на|for) ){Regex.Escape(k)}(?![\p{{L}}\d])", RegexOptions.IgnoreCase)))
            .Where(m => m.Match.Success)
            .OrderBy(m => m.Match.Index)
            .ThenByDescending(m => m.Key.Length)
            .Select(m => Canonical(m.Key))
            .FirstOrDefault();
        if (fromName is not null)
        {
            return fromName;
        }
        return brand.Length < 2 || NotBrands.Contains(brand, StringComparer.OrdinalIgnoreCase) ||
               ProductWord.IsMatch(brand) || ProductWord.IsMatch(Cyrillic(brand)) ? null : brand;
    }

    // "HAУШНИКИ": Latin letters that look Cyrillic, written as Cyrillic for the product-word check.
    static string Cyrillic(string text) =>
        string.Concat(text.ToUpperInvariant().Select(c => "ABCEHKMOPTXY".IndexOf(c) is var i and >= 0 ? "АВСЕНКМОРТХУ"[i] : c));

    static string? Canonical(string brand) =>
        Aliases.TryGetValue(brand, out var alias) ? alias
        : Known.FirstOrDefault(k => string.Equals(k, brand, StringComparison.OrdinalIgnoreCase));
}

static class Photos
{
    const int LongSide = 800;
    const int Quality = 75;

    public static void WriteJpeg(string webp, string jpeg)
    {
        using var original = SKBitmap.Decode(webp) ?? throw new InvalidOperationException($"cannot decode {webp}");
        var scale = Math.Min(1f, (float)LongSide / Math.Max(original.Width, original.Height));
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(original.Width * scale)), Math.Max(1, (int)Math.Round(original.Height * scale)));
        using var resized = scale < 1f ? original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell)) : original.Copy();
        using var data = resized.Encode(SKEncodedImageFormat.Jpeg, Quality);
        File.WriteAllBytes(jpeg, data.ToArray());
    }
}

static class Output
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    const string Schema = "specs/001-test-shop/contracts/product-file.schema.json";

    public static JsonObject Build(List<Category> categories, List<Product> products) => new()
    {
        ["generated"] = false,
        ["source"] = $"Wildberries: Hugging Face dump nyuuzyou/wb-products (CC0) as the index, public card files and photos " +
                     $"for details, fetched {DateTime.UtcNow:yyyy-MM}. Prices and stock are generated. Non-commercial demo; " +
                     "texts and photos belong to their owners. See data/README.md.",
        ["categories"] = new JsonArray([.. categories.Select(c =>
        {
            var node = new JsonObject { ["slug"] = c.Slug, ["name"] = c.Name };
            if (c.Parent is not null)
            {
                node["parent"] = c.Parent;
            }
            return (JsonNode)node;
        })]),
        ["products"] = new JsonArray([.. products.Select(p => (JsonNode)new JsonObject
        {
            ["sku"] = p.Sku,
            ["name"] = p.Name,
            ["category"] = p.Category,
            ["brand"] = p.Brand,
            ["description"] = p.Description,
            ["attributes"] = new JsonObject(p.Attributes.Select(a => KeyValuePair.Create(a.Key, (JsonNode?)a.Value))),
            ["price"] = p.Price,
            ["quantity"] = p.Quantity,
            ["image"] = p.Image,
        })]),
    };

    /// <summary>Schema validation plus the rules the schema cannot express; empty when the catalog is fine.</summary>
    public static List<string> Check(JsonObject document, List<Category> categories, List<Product> products, int? limit)
    {
        var problems = new List<string>();
        var schema = JsonSchema.FromFile(Path.GetFullPath(Schema));
        var result = schema.Evaluate(JsonDocument.Parse(document.ToJsonString()).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!result.IsValid)
        {
            problems.AddRange((result.Details ?? []).Where(d => d.Errors is not null)
                .SelectMany(d => d.Errors!.Select(e => $"schema: {d.InstanceLocation}: {e.Value}")).Take(20));
            if (problems.Count == 0)
            {
                problems.Add("schema: invalid");
            }
        }
        if (products.Select(p => p.Sku).Distinct().Count() != products.Count)
        {
            problems.Add("duplicate SKUs");
        }
        if (categories.Select(c => c.Slug).Distinct().Count() != categories.Count)
        {
            problems.Add("duplicate category slugs");
        }
        if (products.Count == 0)
        {
            problems.Add("no products");
        }
        if (limit is { } n)
        {
            var children = products.Select(p => p.Category).Distinct().Count();
            var brands = products.Select(p => p.Brand).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (products.Count != n)
            {
                problems.Add($"only {products.Count} of {n} products passed the filters; read more dumps (--dumps)");
            }
            if (n >= 100 && children < 5)
            {
                problems.Add($"{children} categories, at least 5 needed");
            }
            if (n >= 100 && brands < 10)
            {
                problems.Add($"{brands} brands, at least 10 needed");
            }
            if (!products.Any(p => p.Quantity == 0) || !products.Any(p => p.Quantity > 0))
            {
                problems.Add("the selection needs both in-stock and out-of-stock products");
            }
        }
        return problems;
    }

    public static void Archive(string directory)
    {
        var path = Path.Combine(directory, "catalog.tar.gz");
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        using var tar = new TarWriter(gzip, TarEntryFormat.Pax);
        tar.WriteEntry(Path.Combine(directory, "catalog.json"), "catalog.json");
        foreach (var image in Directory.GetFiles(Path.Combine(directory, "images")).Order(StringComparer.Ordinal))
        {
            tar.WriteEntry(image, $"images/{Path.GetFileName(image)}");
        }
        Console.WriteLine($"Archived to {path}");
    }
}
