namespace Typeshift.Generators.Models;

// D77: an agent's provider id type, the static type of its WithModel provider, global::-qualified; how its Settings
// store it, and whether it is public (D6)
internal sealed record ProviderIdModel(string Type, StorageModel Storage, bool IsPublic)
{
    // An agent without WithModel, or with a type settings cannot store, which TSH029 reports
    public static readonly ProviderIdModel String = new("string", new StorageModel(StorageKind.Scalar, "string"), true);
}
