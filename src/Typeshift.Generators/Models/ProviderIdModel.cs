namespace Typeshift.Generators.Models;

// D56: the build's provider id type, global::-qualified, how Settings store it, and whether it is public (D6)
internal sealed record ProviderIdModel(string Type, StorageModel Storage, bool IsPublic);
