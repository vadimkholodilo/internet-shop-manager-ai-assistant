using ShopLoader.Validation;

namespace ShopLoader;

/// <summary>What a loader run did, and which records it could not load.</summary>
internal sealed class LoadReport
{
    private readonly List<RecordError> failures = [];

    public int CategoriesCreated { get; private set; }
    public int CategoriesUpdated { get; private set; }
    public int ProductsCreated { get; private set; }
    public int ProductsUpdated { get; private set; }

    public IReadOnlyList<RecordError> Failures => failures;

    public bool Succeeded => failures.Count == 0;

    public void AddFailure(RecordError error) => failures.Add(error);

    public void Category(bool created)
    {
        if (created) { CategoriesCreated++; } else { CategoriesUpdated++; }
    }

    public void Product(bool created)
    {
        if (created) { ProductsCreated++; } else { ProductsUpdated++; }
    }

    public string Summary() =>
        $"Categories: {CategoriesCreated} created, {CategoriesUpdated} updated. " +
        $"Products: {ProductsCreated} created, {ProductsUpdated} updated. " +
        $"Failed records: {failures.Count}.";
}
