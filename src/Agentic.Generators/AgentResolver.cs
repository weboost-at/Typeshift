using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Agentic.Generators.Diagnostics;
using Agentic.Generators.Emitting;
using Agentic.Generators.Models;

namespace Agentic.Generators;

internal static class AgentResolver
{
    public static AgentOutput Resolve(AgentModel agent, ImmutableArray<ToolsetSummary> own, ReferenceModel references)
    {
        var toolsets = own
            .Concat(references.Modules.SelectMany(module => module.Toolsets))
            .GroupBy(toolset => toolset.Type)
            .ToDictionary(group => group.Key, group => group.First());

        var diagnostics = new List<DiagnosticInfo>();
        var tools = new List<ResolvedTool>();

        foreach (var use in agent.Uses)
        {
            // A tool selected twice is offered once; the runtime rejects the duplicate when it reads the agent's tools
            if (toolsets.TryGetValue(use.Toolset, out var toolset))
                tools.AddRange(Select(use, toolset).Where(tool => tools.All(offered => offered.Name != tool.Name)));
            else
                diagnostics.Add(DiagnosticInfo.Create(Descriptors.UngeneratedToolset, use.Location, ShortName(use.Toolset)));
        }

        return new AgentOutput(agent, tools.ToEquatableArray(), diagnostics.ToEquatableArray());
    }

    // A name that picks no tool reads a property the toolset's list does not have, which the compiler reports
    private static IEnumerable<ResolvedTool> Select(ToolsetUseModel use, ToolsetSummary toolset) =>
        (use.Excludes || use.Picked.Count == 0
            ? toolset.Tools.Where(tool => !use.Picked.Contains(Names.ToolProperty(tool.Name)))
            : use.Picked
                .Select(name => toolset.Tools.FirstOrDefault(tool => Names.ToolProperty(tool.Name) == name))
                .OfType<ToolSummary>())
        .Select(tool => new ResolvedTool(use.Toolset, use.IsPublic, tool.Name, tool.Settings));

    private static string ShortName(string type) => type.Substring(type.LastIndexOf('.') + 1);
}
