namespace ShopClient;

/// <summary>Result of an upsert: the shop's id for the item and whether it was created or updated.</summary>
public sealed record UpsertResult(long Id, bool Created);
