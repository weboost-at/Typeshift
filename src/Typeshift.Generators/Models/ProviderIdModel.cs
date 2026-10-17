namespace Typeshift.Generators.Models;

// D56: the build's provider id type, global::-qualified, and how Settings store it
internal sealed record ProviderIdModel(string Type, StorageModel Storage);
