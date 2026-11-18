namespace ShopClient;

/// <summary>
/// A supported kind of shop. Hand-written instead of an enum so that parsing needs no reflection (Native AOT) and the
/// supported names come from one place. <c>default(ShopType)</c> has no name and is not supported.
/// </summary>
public readonly record struct ShopType
{
    private readonly string? name;

    private ShopType(string name) => this.name = name;

    public static ShopType OpenCart { get; } = new("opencart");

    /// <summary>The shop type used when a consumer is not told which shop it works with.</summary>
    public static ShopType Default => OpenCart;

    public static IReadOnlyList<string> SupportedNames { get; } = [OpenCart.Name];

    /// <summary>The name used on command lines and in settings; empty for <c>default(ShopType)</c>.</summary>
    public string Name => name ?? "";

    /// <summary>Case-insensitive.</summary>
    public static bool TryParse(string? text, out ShopType type)
    {
        if (string.Equals(OpenCart.Name, text, StringComparison.OrdinalIgnoreCase))
        {
            type = OpenCart;
            return true;
        }

        type = default;
        return false;
    }

    public override string ToString() => Name;
}
