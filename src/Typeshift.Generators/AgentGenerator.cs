using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators;

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
            .Select(static (toolset, _) => new ToolsetSummary(
                toolset.Type.FullName,
                toolset.Tools
                    .Select(tool => new ToolSummary(tool.Name, tool.Settings, tool.Location))
                    .ToEquatableArray()))
            .Collect();

        var agentTypes = agents
            .Where(static agent => agent.IsPartial)
            .Select(static (agent, _) => agent.Type.FullName)
            .Collect();

        var toolTypes = toolsets
            .Where(static toolset => toolset.IsPartial)
            .SelectMany(static (toolset, _) => GetJsonTypes(toolset))
            .Collect();

        var jsonContexts = classes
            .Where(static model => model!.JsonContext is not null)
            .Select(static (model, _) => model!.JsonContext!)
            .Collect()
            .Combine(toolTypes)
            .Select(static (input, _) => GetJsonContexts(input.Left, input.Right))
            .WithTrackingName(TrackingNames.JsonContexts);

        var module = toolsetSummaries
            .Combine(agentTypes)
            .Combine(jsonContexts)
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? ""))
            .Select(static (input, _) => GetModule(
                input.Left.Left.Left,
                input.Left.Left.Right,
                input.Left.Right,
                input.Right))
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
            module
                .Select(static (module, _) => module?.Name)
                .Combine(references)
                .Combine(calls),
            static (output, input) => EmitInterceptor(output, input.Left.Left, input.Left.Right, input.Right));

        context.RegisterSourceOutput(
            toolsetSummaries
                .Combine(references)
                .Combine(calls),
            static (output, input) => ReportDuplicates(output, input.Left.Left, input.Left.Right, input.Right));
    }

    // The types a tool's arguments and result serialize as
    private static IEnumerable<string> GetJsonTypes(ToolsetModel toolset) =>
        toolset.Tools.SelectMany(tool => tool.Parameters
            .Where(parameter => parameter.Kind == ParameterKind.Argument)
            .Select(parameter => parameter.TypeOf)
            .Append(tool.Result)
            .OfType<string>());

    // D44: a context joins the tool JSON only when it lists a tool's type, so one for other JSON stays out
    private static EquatableArray<string> GetJsonContexts(
        ImmutableArray<JsonContextModel> contexts,
        ImmutableArray<string> toolTypes) =>
        contexts
            .Where(context => context.Types.Any(toolTypes.Contains))
            .Select(context => context.Type)
            .ToEquatableArray();

    private static ModuleModel? GetModule(
        ImmutableArray<ToolsetSummary> toolsets,
        ImmutableArray<string> agents,
        EquatableArray<string> jsonContexts,
        string assemblyName) =>
        toolsets.IsEmpty && agents.IsEmpty
            ? null
            : new ModuleModel(
                ModuleEmitter.GetName(assemblyName),
                assemblyName,
                toolsets.Select(toolset => toolset.Type).ToEquatableArray(),
                agents.ToEquatableArray(),
                jsonContexts);

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

        if (agent.Agent.IsPartial)
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

        var own = module is null ? [] : new[] { Names.Module(module) };
        var modules = own.Concat(references.Modules.Select(reference => reference.Module)).ToList();

        output.AddSource("Interceptors.g.cs", InterceptorEmitter.Emit(calls, modules));
    }

    private static void ReportDuplicates(
        SourceProductionContext output,
        ImmutableArray<ToolsetSummary> toolsets,
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls)
    {
        foreach (var diagnostic in RegistrationDiagnostics.GetDuplicateTools(toolsets, references, calls))
        {
            output.ReportDiagnostic(diagnostic);
        }
    }
}
