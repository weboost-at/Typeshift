using System.Collections.Immutable;
using System.Linq;
using Agentic.Generators.Diagnostics;
using Agentic.Generators.Emitting;
using Agentic.Generators.Models;
using Agentic.Generators.Parsing;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AgentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .CreateSyntaxProvider(Discovery.IsCandidate, Discovery.Classify)
            .Where(static model => model is not null)
            .WithTrackingName(TrackingNames.Classes);

        var toolsets = classes
            .Where(static model => model!.Toolset is not null)
            .Select(static (model, _) => model!.Toolset!)
            .WithTrackingName(TrackingNames.Toolsets);

        var agents = classes
            .Where(static model => model!.Agent is not null)
            .Select(static (model, _) => model!.Agent!)
            .WithTrackingName(TrackingNames.Agents);

        var toolsetSummaries = toolsets
            .Where(static toolset => toolset.IsPartial)
            .Select(static (toolset, _) => Summaries.From(toolset))
            .Collect();

        var agentSummaries = agents
            .Where(static agent => agent.UniqueName is not null)
            .Select(static (agent, _) => Summaries.From(agent))
            .Collect();

        var module = toolsetSummaries
            .Combine(agentSummaries)
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? ""))
            .Select(static (input, _) => GetModule(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.Module);

        var references = context.CompilationProvider
            .Select(ReferenceReader.Read)
            .WithTrackingName(TrackingNames.References);

        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(InterceptParser.IsCandidate, InterceptParser.Parse)
            .Where(static call => call is not null)
            .Select(static (call, _) => call!)
            .Collect()
            .WithTrackingName(TrackingNames.Calls);

        var resolvedAgents = agents
            .Combine(toolsetSummaries.Combine(references))
            .Select(static (input, _) => AgentResolver.Resolve(input.Left, input.Right.Left, input.Right.Right))
            .WithTrackingName(TrackingNames.ResolvedAgents);

        context.RegisterSourceOutput(toolsets, static (output, toolset) => EmitToolset(output, toolset));
        context.RegisterSourceOutput(resolvedAgents, static (output, agent) => EmitAgent(output, agent));
        context.RegisterSourceOutput(module, static (output, module) => EmitModule(output, module));

        context.RegisterSourceOutput(
            module.Select(static (module, _) => module?.Name).Combine(references).Combine(calls),
            static (output, input) => EmitInterceptor(output, input.Left.Left, input.Left.Right, input.Right));

        context.RegisterSourceOutput(
            toolsetSummaries.Combine(agentSummaries).Combine(references).Combine(calls),
            static (output, input) => ReportDuplicates(
                output,
                input.Left.Left.Left,
                input.Left.Left.Right,
                input.Left.Right,
                input.Right));
    }

    private static ModuleModel? GetModule(
        ImmutableArray<ToolsetSummary> toolsets,
        ImmutableArray<AgentSummary> agents,
        string assemblyName) =>
        toolsets.IsEmpty && agents.IsEmpty
            ? null
            : new ModuleModel(
                ModuleEmitter.GetName(assemblyName),
                assemblyName,
                toolsets.ToEquatableArray(),
                agents.ToEquatableArray());

    private static void EmitToolset(SourceProductionContext output, ToolsetModel toolset)
    {
        foreach (var diagnostic in toolset.Diagnostics)
        {
            output.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (toolset.IsPartial) output.AddSource(Names.HintName(toolset.Type.FullName), ToolsetEmitter.Emit(toolset));
    }

    private static void EmitAgent(SourceProductionContext output, AgentOutput agent)
    {
        foreach (var diagnostic in agent.Agent.Diagnostics.Concat(agent.Diagnostics))
        {
            output.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (agent.Agent is { IsPartial: true, UniqueName: not null })
            output.AddSource(Names.HintName(agent.Agent.Type.FullName), AgentEmitter.Emit(agent));
    }

    private static void EmitModule(SourceProductionContext output, ModuleModel? module)
    {
        if (module is not null) output.AddSource("Module.g.cs", ModuleEmitter.Emit(module));
    }

    private static void EmitInterceptor(
        SourceProductionContext output,
        string? module,
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls)
    {
        foreach (var diagnostic in RegistrationDiagnostics.GetUngeneratedReferences(references, calls))
        {
            output.ReportDiagnostic(diagnostic);
        }

        if (calls.IsEmpty) return;

        var own = module is null ? [] : new[] { $"global::Agentic.Generated.{module}" };
        var modules = own.Concat(references.Modules.Select(reference => reference.Module)).ToList();

        output.AddSource("Interceptors.g.cs", InterceptorEmitter.Emit(calls, modules));
    }

    private static void ReportDuplicates(
        SourceProductionContext output,
        ImmutableArray<ToolsetSummary> toolsets,
        ImmutableArray<AgentSummary> agents,
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls)
    {
        var duplicates = RegistrationDiagnostics.GetDuplicateTools(toolsets, references, calls)
            .Concat(RegistrationDiagnostics.GetDuplicateAgents(agents, references, calls));

        foreach (var diagnostic in duplicates)
        {
            output.ReportDiagnostic(diagnostic);
        }
    }
}
