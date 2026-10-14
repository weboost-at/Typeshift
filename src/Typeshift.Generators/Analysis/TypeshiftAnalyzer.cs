using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Analysis;

// D2: every TSH diagnostic, at real locations, so #pragma and .editorconfig reach them
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeshiftAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Descriptors.NotPartial,
        Descriptors.NotConstant,
        Descriptors.NotStaticMethodGroup,
        Descriptors.UnboundParameter,
        Descriptors.SettingsNotRecord,
        Descriptors.SettingsLessAccessible,
        Descriptors.DuplicateToolName,
        Descriptors.NotLowerSnakeCase,
        Descriptors.MissingDescription,
        Descriptors.UnreadSettings,
        Descriptors.DocumentationOff,
        Descriptors.NotStorable,
        Descriptors.NotSettable,
        Descriptors.OutsideConstructor,
        Descriptors.UnreadableChain,
        Descriptors.Unreachable,
        Descriptors.NotDirectlyDerived,
        Descriptors.GeneratedNameClash,
        Descriptors.NotGenerated,
        Descriptors.UnstampedReference,
        Descriptors.OtherIdType,
        Descriptors.SharedKind,
        Descriptors.NotUsableAgent,
        Descriptors.SubAgentNeedsApproval,
        Descriptors.AgentCircle,
        Descriptors.OfferedTwice,
        Descriptors.NoJsonMetadata,
        Descriptors.OtherStoreIdType
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(KnownTypes.Toolset) is null) return;

        var tools = new ConcurrentBag<(string Name, INamedTypeSymbol Toolset, Location Location)>();
        var kinds = new ConcurrentBag<(string Kind, INamedTypeSymbol Agent)>();
        var found = new Found();

        var property = IdTypes.Read(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
        var idType = IdTypes.Resolve(property, context.Compilation);

        context.RegisterSymbolStartAction(start => StartClass(start, tools, kinds, found), SymbolKind.NamedType);
        context.RegisterSymbolAction(symbol => CollectJsonTypes(symbol, found), SymbolKind.NamedType);

        var aot = IsAot(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
        // The library's own builders make the calls a user may not; its built-in toolsets are checked as declarations
        if (context.Compilation.AssemblyName != KnownTypes.LibraryAssembly)
            context.RegisterOperationAction(call => AnalyzeCall(call, idType), OperationKind.Invocation);

        context.RegisterCompilationEndAction(end =>
        {
            ReportClashesAcrossToolsets(end, tools);
            ReportUnstampedReferences(end);
            ReportOtherIdTypes(end, idType);
            ReportSharedKinds(end, kinds);
            ReportAgentUses(end, found);
            if (aot) ReportMissingJsonMetadata(end, found);
        });
    }

    private static void StartClass(
        SymbolStartAnalysisContext context,
        ConcurrentBag<(string Name, INamedTypeSymbol Toolset, Location Location)> tools,
        ConcurrentBag<(string Kind, INamedTypeSymbol Agent)> kinds,
        Found found)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsAbstract) return;

        var baseType = type.DerivesFrom(KnownTypes.Toolset) ? "Toolset"
            : type.DerivesFrom(KnownTypes.Agent) ? "Agent"
            : null;

        if (baseType is null) return;

        var constructors = new ConcurrentBag<IOperation>();

        context.RegisterOperationBlockAction(block =>
        {
            if (block.OwningSymbol is not IMethodSymbol { MethodKind: MethodKind.Constructor }) return;

            foreach (var operation in block.OperationBlocks)
            {
                constructors.Add(operation);
            }
        });

        context.RegisterSymbolEndAction(end =>
        {
            var reporter = new Reporter(end.ReportDiagnostic);

            if (GetIndirection(type, baseType) is { } indirection)
            {
                reporter.Report(
                    Descriptors.NotDirectlyDerived,
                    type.Locations.FirstOrDefault(),
                    type.Name,
                    indirection,
                    baseType);
                return;
            }

            // Constructors come in no particular order; the source order decides which of two clashing
            // declarations is reported
            var ordered = constructors
                .OrderBy(operation => operation.Syntax.SyntaxTree.FilePath)
                .ThenBy(operation => operation.Syntax.SpanStart)
                .ToList();

            if (baseType == "Agent")
            {
                if (AgentParser.Parse(type, ordered, reporter, end.CancellationToken, out var uses) is { } agent)
                {
                    kinds.Add((agent.Kind, type));
                    found.Agents.Add(new FoundAgent(type, agent, uses));
                }

                return;
            }

            var documented = type.DeclaringSyntaxReferences
                .Any(reference => reference.SyntaxTree.Options.DocumentationMode != DocumentationMode.None);

            var ct = end.CancellationToken;
            var toolset = ToolsetParser.Parse(type, ordered, documented, reporter, ct, out var declarations);
            if (toolset is not null) found.Toolsets.Add(Catalog.Summarize(toolset));

            foreach (var (tool, addTool) in declarations)
            {
                tools.Add((tool.Name, type, addTool.Syntax.GetLocation()));
                found.Tools.Add((tool, addTool.Syntax.GetLocation()));
            }
        });
    }

    private static string? GetIndirection(INamedTypeSymbol type, string baseType) =>
        type switch
        {
            { IsGenericType: true } => "is generic",
            { BaseType: var direct } when !direct.Is($"Typeshift.{baseType}") => $"derives from '{direct!.Name}'",
            _ => null
        };

    private static void AnalyzeCall(OperationAnalysisContext context, ITypeSymbol? idType)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var target = invocation.TargetMethod;

        // D11: the store serves ids of the build's type
        if (target is { Name: "WithStore", TypeArguments.Length: 1 }
            && target.ContainingType.Is(KnownTypes.TypeshiftBuilder)
            && target.TypeArguments[0] is var store
            && !store.AllInterfaces.Any(contract => contract.Is(KnownTypes.Store)
                && SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], idType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.OtherStoreIdType,
                ConstructorChains.GetCalledName(invocation).GetLocation(),
                store.Name,
                idType?.ToDisplayString()));

            return;
        }

        var declares = ToolsetParser.IsAddTool(invocation) || AgentParser.IsDeclaration(invocation);

        if (declares && !IsDirectlyInConstructor(invocation, context.ContainingSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.OutsideConstructor,
                ConstructorChains.GetCalledName(invocation).GetLocation(),
                target.Name));

            return;
        }

        // D4: a chain call on a variable goes on a chain the generator never sees
        if (IsChainCall(target) && ConstructorChains.Unwrap(invocation.Instance!) is not IInvocationOperation)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.UnreadableChain,
                invocation.Syntax.GetLocation(),
                "declaration chain"));
        }
    }

    private static readonly string[] Builders =
    [
        KnownTypes.ToolBuilder,
        KnownTypes.SettingsBuilder,
        KnownTypes.ConfigurableSettingsBuilder,
        KnownTypes.ToolsetUse,
        KnownTypes.ToolInclusion,
        KnownTypes.ToolExclusion,
        KnownTypes.ChatOptionsBuilder,
        KnownTypes.RunOptionsBuilder
    ];

    private static bool IsChainCall(IMethodSymbol target) =>
        !target.IsStatic && Builders.Any(builder => target.ContainingType.Is(builder));

    private static bool IsDirectlyInConstructor(IOperation operation, ISymbol containing)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) return false;
        }

        return containing is IMethodSymbol { MethodKind: MethodKind.Constructor };
    }

    // D13: a tool name is unique across everything the host sees, the tools the references stamp too; within one
    // toolset the parser reports it
    private static void ReportClashesAcrossToolsets(
        CompilationAnalysisContext context,
        IEnumerable<(string Name, INamedTypeSymbol Toolset, Location Location)> tools)
    {
        var referenced = new HashSet<string>(ReferenceReader.Read(context.Compilation, context.CancellationToken)
            .Toolsets
            .SelectMany(toolset => toolset.Tools)
            .Select(tool => Names.ToPascalCase(tool.Name)));

        var ordered = tools
            .OrderBy(tool => tool.Location.SourceTree?.FilePath)
            .ThenBy(tool => tool.Location.SourceSpan.Start);

        foreach (var group in ordered.GroupBy(tool => Names.ToPascalCase(tool.Name)))
        {
            var first = group.First();

            var clashes = referenced.Contains(group.Key)
                ? group
                : group.Skip(1).Where(tool => !SymbolEqualityComparer.Default.Equals(tool.Toolset, first.Toolset));

            foreach (var clash in clashes)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Descriptors.DuplicateToolName, clash.Location, clash.Name, group.Key));
            }
        }
    }

    // D9: a reference whose toolsets and agents AddTypeshift() cannot register
    private static void ReportUnstampedReferences(CompilationAnalysisContext context)
    {
        foreach (var assembly in ReferenceReader.GetUnstamped(context.Compilation, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.UnstampedReference, Location.None, assembly.Name));
        }
    }

    private static void ReportOtherIdTypes(CompilationAnalysisContext context, ITypeSymbol? idType)
    {
        foreach (var (assembly, other) in ReferenceReader.GetOtherIdTypes(context.Compilation, idType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.OtherIdType,
                Location.None,
                assembly.Name,
                other.ToDisplayString(),
                idType?.ToDisplayString()));
        }
    }

    // D39: the kinds of this compilation's agents, and those its references stamp
    private static void ReportSharedKinds(
        CompilationAnalysisContext context,
        IEnumerable<(string Kind, INamedTypeSymbol Agent)> kinds)
    {
        var taken = ReferenceReader.GetAgentKinds(context.Compilation)
            .GroupBy(kind => kind.Kind)
            .ToDictionary(group => group.Key, group => group.First().Agent);

        var ordered = kinds
            .OrderBy(kind => kind.Agent.Locations.FirstOrDefault()?.SourceTree?.FilePath)
            .ThenBy(kind => kind.Agent.Locations.FirstOrDefault()?.SourceSpan.Start);

        foreach (var (kind, agent) in ordered)
        {
            if (taken.TryGetValue(kind, out var other))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.SharedKind,
                    agent.Locations.FirstOrDefault(),
                    agent.ToDisplayString(),
                    kind,
                    other));

                continue;
            }

            taken[kind] = agent.ToDisplayString();
        }
    }

    // D33, D34: the agents an agent uses, as far as this compilation declares them
    private static void ReportAgentUses(CompilationAnalysisContext context, Found found)
    {
        var agents = found.Agents.ToDictionary(
            agent => agent.Type,
            agent => agent,
            (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default);

        List<ToolsetSummary> toolsets =
        [
            .. found.Toolsets,
            .. ReferenceReader.Read(context.Compilation, context.CancellationToken).Toolsets
        ];

        foreach (var (type, _, uses) in found.Agents)
        {
            foreach (var use in uses)
            {
                if (!agents.TryGetValue(use.Target, out var target)) continue;

                var role = use.SubAgent ? "a sub-agent" : "a handoff target";

                if (target.Model.Description is null)
                {
                    var reason = "it has no WithDescription";
                    Report(context, Descriptors.NotUsableAgent, use, target.Type.Name, role, reason);
                }
                else if (!ToolNames.IsLowerSnakeCase(target.Model.Name))
                {
                    var reason = $"its name '{target.Model.Name}' is not lower_snake_case";
                    Report(context, Descriptors.NotUsableAgent, use, target.Type.Name, role, reason);
                }

                if (!use.SubAgent) continue;

                var asking = Catalog.Resolve(target.Model, toolsets).Tools
                    .Where(offered => offered.Tool.RequiresApproval)
                    .Select(offered => offered.Tool.Name)
                    .ToList();

                if (asking.Count > 0)
                {
                    var names = string.Join(", ", asking);
                    Report(context, Descriptors.SubAgentNeedsApproval, use, target.Type.Name, names);
                }

                if (Calls(use.Target, type, agents))
                    Report(context, Descriptors.AgentCircle, use, type.Name, target.Type.Name);
            }
        }
    }

    // Whether an agent reaches another through sub-agents
    private static bool Calls(
        INamedTypeSymbol from,
        INamedTypeSymbol to,
        Dictionary<INamedTypeSymbol, FoundAgent> agents)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var next = new Stack<INamedTypeSymbol>([from]);

        while (next.Count > 0)
        {
            var agent = next.Pop();
            if (SymbolEqualityComparer.Default.Equals(agent, to)) return true;
            if (!seen.Add(agent) || !agents.TryGetValue(agent, out var found)) continue;

            foreach (var use in found.Uses.Where(use => use.SubAgent))
            {
                next.Push(use.Target);
            }
        }

        return false;
    }

    private static void Report(
        CompilationAnalysisContext context,
        DiagnosticDescriptor descriptor,
        AgentUseDeclaration use,
        params object[] arguments) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, use.Syntax.GetLocation(), arguments));

    // D45: Microsoft.Extensions.AI's own context covers these, without reflection
    private static readonly HashSet<string> CoveredTypes =
    [
        "string", "char", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "bool",
        "global::System.TimeSpan", "global::System.DateTime", "global::System.DateTimeOffset",
        "global::System.Text.Json.JsonElement", "global::System.Text.Json.JsonDocument",
        "global::System.Text.Json.Nodes.JsonNode", "global::System.Text.Json.Nodes.JsonObject",
        "global::System.Text.Json.Nodes.JsonArray", "global::System.Text.Json.Nodes.JsonValue",
        "global::System.Collections.Generic.IEnumerable<string>"
    ];

    private static bool IsAot(AnalyzerConfigOptions options) =>
        new[] { "build_property.PublishAot", "build_property.IsAotCompatible" }
            .Any(name => options.TryGetValue(name, out var value) && IsTrue(value));

    private static bool IsTrue(string value) => value.Equals("true", System.StringComparison.OrdinalIgnoreCase);

    private static void CollectJsonTypes(SymbolAnalysisContext context, Found found)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!type.DerivesFrom(KnownTypes.JsonSerializerContext)) return;

        var listed = type.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.JsonSerializableAttribute))
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value)
            .OfType<ITypeSymbol>();

        foreach (var json in listed)
        {
            found.JsonTypes[json.ToTypeOf()] = true;
        }
    }

    // D45: under Native AOT, every type a tool binds or returns needs metadata from a context
    private static void ReportMissingJsonMetadata(CompilationAnalysisContext context, Found found)
    {
        foreach (var (tool, location) in found.Tools)
        {
            var types = tool.Parameters
                .Where(parameter => parameter.Kind == ParameterKind.Argument)
                .Select(parameter => parameter.TypeOf)
                .Concat(tool.Result is { } result ? [result] : []);

            foreach (var type in types.Distinct())
            {
                if (CoveredTypes.Contains(type) || found.JsonTypes.ContainsKey(type)) continue;

                context.ReportDiagnostic(Diagnostic.Create(Descriptors.NoJsonMetadata, location, type, tool.Name));
            }
        }
    }

    // What the classes of the compilation declare, for the checks at its end
    private sealed class Found
    {
        public ConcurrentBag<(ToolModel Tool, Location Location)> Tools { get; } = [];

        public ConcurrentDictionary<string, bool> JsonTypes { get; } = [];

        public ConcurrentBag<FoundAgent> Agents { get; } = [];

        public ConcurrentBag<ToolsetSummary> Toolsets { get; } = [];
    }

    private sealed record FoundAgent(INamedTypeSymbol Type, AgentModel Model, IReadOnlyList<AgentUseDeclaration> Uses);
}
