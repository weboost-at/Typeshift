namespace Typeshift.Generators.Models;

// D7: what an agent needs of a toolset, from its model in this compilation or from the stamps of a reference
internal sealed record ToolsetSummary(string Type, bool IsPublic, EquatableArray<ToolSummary> Tools);

// Settings and Overrides are global::-qualified type names; Members the settings' settable members, which
// Configure{Tool}() sets
internal sealed record ToolSummary(
    string Name,
    string Property,
    string Factory,
    string? Settings,
    string? Overrides,
    EquatableArray<SettingMember> Members,
    bool RequiresApproval = false);

internal sealed record SettingMember(string Name, string Type);

// An agent with its toolset uses resolved to the tools they offer
internal sealed record AgentOutput(AgentModel Agent, EquatableArray<OfferedTool> Tools);

internal sealed record OfferedTool(string Toolset, ToolSummary Tool);
