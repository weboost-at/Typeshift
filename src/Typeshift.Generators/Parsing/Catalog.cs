using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Parsing;

// What agents pick from: the toolsets of this compilation and those the references stamp
internal static class Catalog
{
    public static ToolsetSummary Summarize(ToolsetModel toolset) =>
        new(
            toolset.Type.FullName,
            toolset.Type.Accessibility == "public",
            toolset.Tools.Select(tool => Summarize(toolset.Type, tool)).ToEquatableArray());

    // D33, D34: the agent as its module stamps it, for the agents that use it
    public static ModuleAgent Summarize(AgentOutput output)
    {
        var approvalTools = output.Tools
            .Where(offered => offered.Tool.RequiresApproval)
            .Select(offered => offered.Tool.Name)
            .ToEquatableArray();

        return new ModuleAgent(
            Type: output.Agent.Type.FullName,
            Kind: output.Agent.Kind,
            Name: output.Agent.Name,
            Described: output.Agent.Description is not null,
            ApprovalTools: approvalTools,
            Tools: output.Tools.Select(offered => offered.Tool.Name).ToEquatableArray(),
            OwnTools: output.Tools
                .Where(offered => offered.Owner == output.Agent.Type.FullName)
                .Select(offered => offered.Tool.Name)
                .ToEquatableArray());
    }

    // A use of a toolset the catalog lacks is skipped (TSH019), and so is a tool already offered (TSH025, or a name
    // two referenced toolsets share), so the generated members stay unique
    public static AgentOutput Resolve(AgentModel agent, IReadOnlyList<ToolsetSummary> toolsets)
    {
        var offered = new List<OfferedTool>();

        foreach (var use in agent.Toolsets)
        {
            if (toolsets.FirstOrDefault(toolset => toolset.Type == use.Toolset) is not { } toolset) continue;

            var tools = Offered(use, toolset);

            // TSH006: a public agent's Settings cannot name the overrides of a toolset less accessible
            var hidden = agent.Type.Accessibility == "public" && !toolset.IsPublic;

            foreach (var tool in tools)
            {
                if (offered.Any(other => Clashes(other.Tool, tool))) continue;

                offered.Add(new OfferedTool(toolset.Type, hidden ? tool with { Overrides = null } : tool));
            }
        }

        // D57: the agent's own tools come after its toolsets'
        foreach (var tool in agent.Tools.Select(own => Summarize(agent.Type, own)))
        {
            if (offered.Any(other => Clashes(other.Tool, tool))) continue;

            offered.Add(new OfferedTool(agent.Type.FullName, tool));
        }

        return new AgentOutput(agent, offered.ToEquatableArray());
    }

    // Included tools come in the order picked, the others in the toolset's
    public static IEnumerable<ToolSummary> Offered(ToolsetUseModel use, ToolsetSummary toolset) =>
        use.Excludes || use.Picks.Count == 0
            ? toolset.Tools.Where(tool => AgentParser.Offers(use.Excludes, use.Picks, tool.Property))
            : use.Picks.Select(pick => toolset.Tools.FirstOrDefault(tool => tool.Property == pick))
                .OfType<ToolSummary>();

    // The generated members of a tool live in the toolset or the agent that declares it
    private static ToolSummary Summarize(TypeModel owner, ToolModel tool) =>
        new(
            Name: tool.Name,
            Property: Names.ToolProperty(tool.Name),
            Factory: Names.FactoryName(tool.Name),
            Settings: tool.Settings?.Type,
            Overrides: tool.Settings?.HasConfigurable is true
                ? $"{owner.FullName}.{Names.OverridesName(tool.Name)}"
                : null,
            Members: tool.Settings?.Properties
                .Select(property => new SettingMember(property.Name, property.Type))
                .ToEquatableArray() ?? default,
            RequiresApproval: tool.RequiresApproval);

    private static bool Clashes(ToolSummary offered, ToolSummary tool) =>
        offered.Name == tool.Name || Names.ToPascalCase(offered.Name) == Names.ToPascalCase(tool.Name);
}
