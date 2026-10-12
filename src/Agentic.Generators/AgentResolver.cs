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
            if (toolsets.TryGetValue(use.Toolset, out var toolset))
                tools.AddRange(Select(use, toolset).Where(tool => !Offers(tools, tool)));
            else
                diagnostics.Add(DiagnosticInfo.Create(Descriptors.UngeneratedToolset, use.Location, ShortName(use.Toolset)));
        }

        return new AgentOutput(agent, tools.ToEquatableArray(), diagnostics.ToEquatableArray());
    }

    // A tool selected twice is offered once; the runtime rejects the duplicate as it runs the constructor
    private static bool Offers(IEnumerable<ResolvedTool> tools, ResolvedTool tool) =>
        tools.Any(offered => offered.Name == tool.Name);

    // A name that selects no tool is a call to a selection method that does not exist, which the compiler reports
    private static IEnumerable<ResolvedTool> Select(ToolsetUseModel use, ToolsetSummary toolset) =>
        (use.Selected.Count == 0
            ? toolset.Tools
            : use.Selected
                .Select(name => toolset.Tools.FirstOrDefault(tool => Names.SelectorName(tool.Name) == name))
                .OfType<ToolSummary>())
        .Select(tool => new ResolvedTool(use.Toolset, use.IsPublic, tool.Name, tool.Settings));

    private static string ShortName(string type) => type.Substring(type.LastIndexOf('.') + 1);
}
