namespace Typeshift.Generators.Models;

// D7: what an agent needs of a toolset, from its model in this compilation or from the stamps of a reference
internal sealed record ToolsetSummary(string Type, bool IsPublic, EquatableArray<ToolSummary> Tools);

// Settings and Overrides are global::-qualified type names; Members the settings' settable members, which
// Configure{Tool}() sets; ExperimentalIds are those of the settings
internal sealed record ToolSummary(
    string Name,
    string Property,
    string Factory,
    string? Settings,
    string? Overrides,
    EquatableArray<SettingMember> Members,
    bool RequiresApproval,
    EquatableArray<string> ExperimentalIds);

// Summary is the member's <summary>, which its With{Member}() shows
internal sealed record SettingMember(string Name, string Type, string? Summary);

// An agent with its toolset uses resolved to the tools they offer
internal sealed record AgentOutput(AgentModel Agent, EquatableArray<OfferedTool> Tools);

// Owner is the toolset or, for a tool of the agent's own, the agent that declares the tool (D57). AsksFirst: the
// agent's use requires approval for a tool its toolset does not, which the agent wraps; Switchable: settings may
// switch the tool off (D84)
internal sealed record OfferedTool(string Owner, ToolSummary Tool, bool AsksFirst = false, bool Switchable = false);
