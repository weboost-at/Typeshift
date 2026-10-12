namespace Agentic.Generators.Models;

// What other classes and compilations need to know of a toolset, own or read from a reference's stamps
internal sealed record ToolsetSummary(string Type, EquatableArray<ToolSummary> Tools);

// Location is null for a tool of a referenced assembly
internal sealed record ToolSummary(
    string Name,
    string Method,
    string? Settings,
    EquatableArray<string> Configurable,
    LocationInfo? Location);
