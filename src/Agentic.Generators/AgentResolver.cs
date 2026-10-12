using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Agentic.Generators.Diagnostics;
using Agentic.Generators.Models;

namespace Agentic.Generators;

// Resolves an agent's toolset selections against the own toolsets and the stamped ones of its references
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
        var toolCountAfterUse = new List<int>();

        foreach (var use in agent.Uses)
        {
            if (toolsets.TryGetValue(use.Toolset, out var toolset))
                tools.AddRange(Select(use, toolset, diagnostics).Where(tool => !Offers(tools, tool)));
            else
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.UnknownTool,
                    use.Location,
                    $"{ShortName(use.Toolset)} was not compiled with the generator, so its tools are unknown"));

            toolCountAfterUse.Add(tools.Count);
        }

        foreach (var settings in agent.ToolSettings)
        {
            var addedBefore = settings.UsesBefore == 0 ? [] : tools.Take(toolCountAfterUse[settings.UsesBefore - 1]);

            if (!addedBefore.Any(tool => tool.Settings == settings.Type))
                diagnostics.Add(
                    DiagnosticInfo.Create(Descriptors.UnreadToolSettings, settings.Location, ShortName(settings.Type)));
        }

        return new AgentOutput(agent, tools.ToEquatableArray(), diagnostics.ToEquatableArray());
    }

    // A tool selected twice is offered once; the runtime rejects the duplicate as it runs the constructor
    private static bool Offers(IEnumerable<ResolvedTool> tools, ResolvedTool tool) =>
        tools.Any(offered => offered.Name == tool.Name);

    private static IEnumerable<ResolvedTool> Select(
        ToolsetUseModel use,
        ToolsetSummary toolset,
        List<DiagnosticInfo> diagnostics)
    {
        if (use.All) return toolset.Tools.Select(tool => Resolved(use, tool));

        var selected = new List<ResolvedTool>();

        foreach (var method in use.Methods)
        {
            if (toolset.Tools.FirstOrDefault(tool => tool.Method == method.Name) is { } tool)
                selected.Add(Resolved(use, tool));
            else
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.UnknownTool,
                    method.Location,
                    $"{method.Name} is no tool of {ShortName(toolset.Type)}"));
        }

        return selected;
    }

    private static ResolvedTool Resolved(ToolsetUseModel use, ToolSummary tool) =>
        new(use.Toolset, use.IsPublic, tool.Name, tool.Settings);

    private static string ShortName(string type) => type.Substring(type.LastIndexOf('.') + 1);
}
