using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        var annotations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypes.SchemaAnnotationsAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                Discovery.ClassifySchemaAnnotations)
            .Where(static name => name is not null)
            .Select(static (name, _) => name!)
            .Collect();

        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(InterceptParser.IsCandidate, InterceptParser.Parse)
            .Where(static call => call is not null)
            .Select(static (call, _) => call!)
            .Collect();

        // D56: the provider id type of the Settings members, the alias, the stamps and the core services
        var providerId = context.AnalyzerConfigOptionsProvider
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => IdTypes.Provider(pair.Left.GlobalOptions, pair.Right));

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

        // The library runs its own generator and needs no aliases of its types
        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "");

        var module = toolsets
            .Collect()
            .Combine(outputs.Collect())
            .Combine(contexts)
            .Combine(annotations)
            .Combine(assemblyName)
            .Combine(providerId)
            .Select(static (pair, _) => CreateModule(
                pair.Left.Left.Left.Left.Left,
                pair.Left.Left.Left.Left.Right,
                pair.Left.Left.Left.Right,
                pair.Left.Left.Right,
                pair.Left.Right,
                pair.Right.Type))
            .WithTrackingName(TrackingNames.Module);

        context.RegisterSourceOutput(toolsets, static (output, toolset) =>
            output.AddSource(Names.HintName(toolset.Type.FullName), ToolsetEmitter.Emit(toolset)));

        context.RegisterSourceOutput(outputs.Combine(providerId), static (output, pair) =>
        {
            var agent = pair.Left;
            output.AddSource(Names.HintName(agent.Agent.Type.FullName), AgentEmitter.Emit(agent, pair.Right));

            if (AgentEmitter.EmitHost(agent.Agent) is { } host)
                output.AddSource(Names.HintName($"{agent.Agent.Type.FullName}+Host"), host);
        });

        context.RegisterSourceOutput(providerId.Combine(assemblyName), static (output, pair) =>
        {
            if (pair.Right == KnownTypes.LibraryAssembly) return;

            output.AddSource(
                Names.HintName($"{Names.GeneratedNamespace}.Aliases"),
                AliasEmitter.Emit(pair.Left.Type));
        });

        context.RegisterSourceOutput(module, static (output, model) =>
        {
            if (model is null) return;

            output.AddSource(Names.HintName($"{Names.GeneratedNamespace}.{model.Name}"), ModuleEmitter.Emit(model));
        });

        context.RegisterSourceOutput(
            calls.Combine(module).Combine(references).Combine(providerId),
            static (output, pair) => EmitInterceptor(
                output,
                pair.Left.Left.Left,
                pair.Left.Left.Right,
                pair.Left.Right,
                pair.Right.Type));
    }

    // D55: an assembly that only documents types still gets a module, so the tools of its referrers describe them
    private static ModuleModel? CreateModule(
        ImmutableArray<ToolsetModel> toolsets,
        ImmutableArray<AgentOutput> outputs,
        ImmutableArray<string> contexts,
        ImmutableArray<string> annotations,
        string assemblyName,
        string providerIdType)
    {
        if (toolsets.IsEmpty && outputs.IsEmpty && annotations.IsEmpty) return null;

        var reachableToolsets = toolsets
            .Where(toolset => toolset.IsReachable)
            .Select(toolset => toolset.Type.FullName)
            .ToEquatableArray();

        var reachableAgents = outputs
            .Where(output => output.Agent.IsReachable)
            .Select(Catalog.Summarize)
            .ToEquatableArray();

        return new ModuleModel(
            Name: ModuleEmitter.GetName(assemblyName),
            AssemblyName: assemblyName,
            ProviderIdType: providerIdType,
            Toolsets: reachableToolsets,
            Agents: reachableAgents,
            JsonContexts: contexts.Distinct().ToEquatableArray(),
            SchemaAnnotations: annotations.ToEquatableArray());
    }

    private static void EmitInterceptor(
        SourceProductionContext output,
        ImmutableArray<InterceptModel> calls,
        ModuleModel? module,
        ReferenceModel references,
        string providerIdType)
    {
        if (calls.IsEmpty) return;

        IEnumerable<string> own = module is null ? [] : [$"global::{Names.GeneratedNamespace}.{module.Name}"];

        output.AddSource(
            Names.HintName($"{Names.GeneratedNamespace}.{KnownTypes.RegistrationMethod}Interceptor"),
            InterceptorEmitter.Emit(calls, [.. own, .. references.Modules], providerIdType));
    }
}
