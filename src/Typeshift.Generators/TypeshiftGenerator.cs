using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators;

// D2: writes code only; the analyzer reports every diagnostic
[Generator(LanguageNames.CSharp)]
public sealed class TypeshiftGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolsets = context.SyntaxProvider
            .CreateSyntaxProvider(Discovery.IsToolset, Discovery.ClassifyToolset)
            .Where(static toolset => toolset is not null)
            .Select(static (toolset, _) => toolset!)
            .WithTrackingName(TrackingNames.Toolsets);

        var agents = context.SyntaxProvider
            .CreateSyntaxProvider(Discovery.IsAgent, Discovery.ClassifyAgent)
            .Where(static agent => agent is not null)
            .Select(static (agent, _) => agent!)
            .WithTrackingName(TrackingNames.Agents);

        var contexts = context.SyntaxProvider
            .CreateSyntaxProvider(Discovery.IsJsonContext, Discovery.ClassifyJsonContext)
            .Where(static name => name is not null)
            .Select(static (name, _) => name!)
            .Collect();

        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(InterceptParser.IsCandidate, InterceptParser.Parse)
            .Where(static call => call is not null)
            .Select(static (call, _) => call!)
            .Collect();

        // Every edit makes a new compilation; the stamps it yields compare equal, so what depends on them is cached
        var references = context.CompilationProvider
            .Select(ReferenceReader.Read)
            .WithTrackingName(TrackingNames.References);

        var catalog = toolsets
            .Select(static (toolset, _) => Catalog.Summarize(toolset))
            .Collect()
            .Combine(references)
            .Select(static (pair, _) => pair.Left.Concat(pair.Right.Toolsets).ToEquatableArray());

        var outputs = agents
            .Combine(catalog)
            .Select(static (pair, _) => Catalog.Resolve(pair.Left, pair.Right))
            .WithTrackingName(TrackingNames.AgentOutputs);

        var module = toolsets
            .Collect()
            .Combine(agents.Collect())
            .Combine(contexts)
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? ""))
            .Select(static (pair, _) =>
                CreateModule(pair.Left.Left.Left, pair.Left.Left.Right, pair.Left.Right, pair.Right))
            .WithTrackingName(TrackingNames.Module);

        context.RegisterSourceOutput(toolsets, static (output, toolset) =>
            output.AddSource(Names.HintName(toolset.Type.FullName), ToolsetEmitter.Emit(toolset)));

        context.RegisterSourceOutput(outputs, static (output, agent) =>
            output.AddSource(Names.HintName(agent.Agent.Type.FullName), AgentEmitter.Emit(agent)));

        context.RegisterSourceOutput(module, static (output, model) =>
        {
            if (model is null) return;

            output.AddSource(Names.HintName($"{Names.GeneratedNamespace}.{model.Name}"), ModuleEmitter.Emit(model));
        });

        context.RegisterSourceOutput(calls.Combine(module).Combine(references), static (output, pair) =>
            EmitInterceptor(output, pair.Left.Left, pair.Left.Right, pair.Right));
    }

    // Null for an assembly that declares neither toolsets nor agents; its contexts serve no generated tool
    private static ModuleModel? CreateModule(
        ImmutableArray<ToolsetModel> toolsets,
        ImmutableArray<AgentModel> agents,
        ImmutableArray<string> contexts,
        string assemblyName)
    {
        if (toolsets.IsEmpty && agents.IsEmpty) return null;

        return new ModuleModel(
            ModuleEmitter.GetName(assemblyName),
            assemblyName,
            toolsets.Where(toolset => toolset.IsReachable).Select(toolset => toolset.Type.FullName).ToEquatableArray(),
            agents.Where(agent => agent.IsReachable).Select(agent => agent.Type.FullName).ToEquatableArray(),
            contexts.Distinct().ToEquatableArray());
    }

    private static void EmitInterceptor(
        SourceProductionContext output,
        ImmutableArray<InterceptModel> calls,
        ModuleModel? module,
        ReferenceModel references)
    {
        if (calls.IsEmpty) return;

        IEnumerable<string> own = module is null ? [] : [$"global::{Names.GeneratedNamespace}.{module.Name}"];

        output.AddSource(
            Names.HintName($"{Names.GeneratedNamespace}.{KnownTypes.RegistrationMethod}Interceptor"),
            InterceptorEmitter.Emit(calls, [.. own, .. references.Modules]));
    }
}
