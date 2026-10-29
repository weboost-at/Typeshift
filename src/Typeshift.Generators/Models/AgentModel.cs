namespace Typeshift.Generators.Models;

// IsReachable: code outside the class can name and create it, so the host members and the module reach it.
// ChatOptions and RunOptions hold the configurable options' member names, Tools those it declares itself (D57),
// Configures the Configure{Tool} names the user declared, which the generator leaves to them; Kind and Version are
// those of the stored Settings
internal sealed record AgentModel(
    TypeModel Type,
    bool IsReachable,
    string Name,
    string? Description,
    string Kind,
    int Version,
    EquatableArray<string> ChatOptions,
    EquatableArray<string> RunOptions,
    EquatableArray<ToolsetUseModel> Toolsets,
    EquatableArray<ToolModel> Tools,
    EquatableArray<string> Configures);

// Picks are tool list property names, in source order; with none, the use offers every tool
internal sealed record ToolsetUseModel(string Toolset, bool Excludes, EquatableArray<string> Picks);
