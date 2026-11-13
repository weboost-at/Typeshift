using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
        Descriptors.NotBindable,
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
        Descriptors.SharedKind,
        Descriptors.NotUsableAgent,
        Descriptors.SubAgentNeedsApproval,
        Descriptors.AgentCircle,
        Descriptors.OfferedTwice,
        Descriptors.NoJsonMetadata,
        Descriptors.NoStore,
        Descriptors.UnusableProviderIdType,
        Descriptors.NoChatClientProvider
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

        var tools = new ConcurrentBag<(string Name, INamedTypeSymbol Owner, Location Location)>();
        var kinds = new ConcurrentBag<(string Kind, INamedTypeSymbol Agent)>();
        var found = new Found();

        context.RegisterSymbolStartAction(start => StartClass(start, tools, kinds, found), SymbolKind.NamedType);

        context.RegisterSymbolAction(symbol => CollectJsonTypes(symbol, found), SymbolKind.NamedType);

        var aot = IsAot(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
        // The library's own builders make the calls a user may not
        if (context.Compilation.AssemblyName != KnownTypes.LibraryAssembly)
            context.RegisterOperationAction(AnalyzeCall, OperationKind.Invocation);

        context.RegisterCompilationEndAction(end =>
        {
            ReportClashesAcrossToolsets(end, tools);
            ReportUnstampedReferences(end);
            ReportSharedKinds(end, kinds);
            ReportAgentUses(end, found);
            if (aot) ReportMissingJsonMetadata(end, found);
        });
    }

    private static void StartClass(
        SymbolStartAnalysisContext context,
        ConcurrentBag<(string Name, INamedTypeSymbol Owner, Location Location)> tools,
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
            var ct = end.CancellationToken;

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

            var documented = type.DeclaringSyntaxReferences
                .Any(reference => reference.SyntaxTree.Options.DocumentationMode != DocumentationMode.None);

            var declarations = baseType == "Agent"
                ? ParseAgent(type, ordered, documented, reporter, kinds, found, ct)
                : ParseToolset(type, ordered, documented, reporter, found, ct);

            foreach (var (tool, addTool) in declarations)
            {
                var location = addTool.Syntax.GetLocation();
                found.Tools.Add((tool, location));

                // D57: an agent's own tools clash only across the handoffs of a conversation
                if (baseType == "Agent") found.OwnTools.Add((type, tool.Name, location));
                else tools.Add((tool.Name, type, location));
            }
        });
    }

    private static IReadOnlyList<(ToolModel Tool, IInvocationOperation AddTool)> ParseAgent(
        INamedTypeSymbol type,
        IReadOnlyList<IOperation> constructors,
        bool documented,
        Reporter reporter,
        ConcurrentBag<(string Kind, INamedTypeSymbol Agent)> kinds,
        Found found,
        CancellationToken ct)
    {
        var agent = AgentParser.Parse(type, constructors, documented, reporter, ct, out var uses, out var tools);

        if (agent is null) return tools;

        kinds.Add((agent.Kind, type));
        found.Agents.Add(new FoundAgent(type, agent, uses));
        return tools;
    }

    private static IReadOnlyList<(ToolModel Tool, IInvocationOperation AddTool)> ParseToolset(
        INamedTypeSymbol type,
        IReadOnlyList<IOperation> constructors,
        bool documented,
        Reporter reporter,
        Found found,
        CancellationToken ct)
    {
        var toolset = ToolsetParser.Parse(type, constructors, documented, reporter, ct, out var tools);
        if (toolset is not null) found.Toolsets.Add(Catalog.Summarize(toolset));

        return tools;
    }

    private static string? GetIndirection(INamedTypeSymbol type, string baseType) =>
        type switch
        {
            { IsGenericType: true } => "is generic",
            { BaseType: var direct } when !direct.Is($"Typeshift.{baseType}") => $"derives from '{direct!.Name}'",
            _ => null
        };

    private static void AnalyzeCall(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var target = invocation.TargetMethod;

        // D77: a chat client provider may serve providers of any type, but it must serve some. A type parameter is
        // its caller's to check, since its interfaces are unknown here, and a type that does not resolve the compiler's
        if (target is { Name: "WithChatClientProvider", TypeArguments.Length: 1 }
            && target.ContainingType.Is(KnownTypes.TypeshiftBuilder)
            && target.TypeArguments[0] is { TypeKind: not (TypeKind.TypeParameter or TypeKind.Error) } clients
            && !clients.AllInterfaces.Any(contract => contract.Is(KnownTypes.ChatClientProvider)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.NoChatClientProvider,
                ConstructorChains.GetCalledName(invocation).GetLocation(),
                clients.Name));

            return;
        }

        // D72: a store of any id type serves the ids of its type; a type parameter is its caller's to check, and a
        // type that does not resolve the compiler's
        if (target is { Name: "WithStore", TypeArguments.Length: 1 }
            && target.ContainingType.Is(KnownTypes.TypeshiftBuilder)
            && target.TypeArguments[0] is { TypeKind: not (TypeKind.TypeParameter or TypeKind.Error) } store
            && !IsStore(store))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.NoStore,
                ConstructorChains.GetCalledName(invocation).GetLocation(),
                store.Name));

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

    // D38, D66, D72: a store keeps settings or conversations, of any id type, or agents' own settings
    private static bool IsStore(ITypeSymbol store)
    {
        // A store your code holds as one of the interfaces is that interface too
        IEnumerable<ITypeSymbol> implemented = store.TypeKind == TypeKind.Interface
            ? [store, .. store.AllInterfaces]
            : store.AllInterfaces;

        return implemented.Any(contract => contract.Is(KnownTypes.SettingsStore)
            || contract.Is(KnownTypes.ConversationStore)
            || contract.Is(KnownTypes.AgentSettingsStore));
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

    // D13: a toolset's tool name is unique across everything the host sees, the tools the references stamp too;
    // within one toolset or agent the parser reports it
    private static void ReportClashesAcrossToolsets(
        CompilationAnalysisContext context,
        IEnumerable<(string Name, INamedTypeSymbol Owner, Location Location)> tools)
    {
        var referenced = ReferenceReader
            .Read(context.Compilation, context.CancellationToken)
            .Toolsets
            .SelectMany(toolset => toolset.Tools)
            .ToLookup(tool => Names.ToPascalCase(tool.Name));

        foreach (var group in referenced)
        {
            foreach (var clash in group.Skip(1))
            {
                // A referenced tool has no location, so a clash between two references goes on the compilation
                context.ReportDiagnostic(
                    Diagnostic.Create(Descriptors.DuplicateToolName, Location.None, clash.Name, group.Key));
            }
        }

        var ordered = tools
            .OrderBy(tool => tool.Location.SourceTree?.FilePath)
            .ThenBy(tool => tool.Location.SourceSpan.Start);

        foreach (var group in ordered.GroupBy(tool => Names.ToPascalCase(tool.Name)))
        {
            var first = group.First();

            var clashes = referenced.Contains(group.Key)
                ? group
                : group.Skip(1).Where(tool => !SymbolEqualityComparer.Default.Equals(tool.Owner, first.Owner));

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
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.UnstampedReference,
                Location.None,
                assembly.Name,
                "was built with another Typeshift version or without the Typeshift package",
                "reference the Typeshift package in its project, at this version, and build it again"));
        }

        foreach (var assembly in ReferenceReader.GetUnderAliasOnly(context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.UnstampedReference,
                Location.None,
                assembly.Name,
                "is referenced under an extern alias only, which generated code cannot name",
                "reference it without an alias"));
        }
    }

    // D39: the kinds of this compilation's agents, and those its references stamp
    private static void ReportSharedKinds(
        CompilationAnalysisContext context,
        IEnumerable<(string Kind, INamedTypeSymbol Agent)> kinds)
    {
        // A referenced agent has no location here, so a clash between two references is reported on the compilation
        var referenced = ReferenceReader
            .GetAgentKinds(context.Compilation)
            .Select(stamped => (stamped.Kind, stamped.Agent, Location: Location.None));

        var own = kinds
            .Select(kind => (kind.Kind, Agent: kind.Agent.ToDisplayString(), Location: kind.Agent.Locations.First()))
            .OrderBy(kind => kind.Location.SourceTree?.FilePath)
            .ThenBy(kind => kind.Location.SourceSpan.Start);

        var taken = new Dictionary<string, string>();

        // References first, so a clash with an agent of this compilation is reported on that agent
        foreach (var (kind, agent, location) in referenced.Concat(own))
        {
            if (taken.TryGetValue(kind, out var other))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.SharedKind, location, agent, kind, other));
                continue;
            }

            taken[kind] = agent;
        }
    }

    // D33, D34: the agents an agent uses, of this compilation or of a reference, whose module stamps them
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

        var referenced = ReferenceReader
            .GetAgents(context.Compilation)
            .ToLookup(agent => agent.Type);

        foreach (var (type, _, uses) in found.Agents)
        {
            foreach (var use in uses)
            {
                if (ResolveTarget(use.Target) is not { } target) continue;

                var role = use.SubAgent ? "a sub-agent" : "a handoff target";

                if (GetUnusableReason(target) is { } reason)
                {
                    Report(context, Descriptors.NotUsableAgent, use, use.Target.Name, role, reason);
                }

                if (!use.SubAgent) continue;

                if (target.ApprovalTools.Count > 0)
                {
                    var names = string.Join(", ", target.ApprovalTools);
                    Report(context, Descriptors.SubAgentNeedsApproval, use, use.Target.Name, names);
                }

                // Assemblies cannot reference each other in a circle, so only this compilation's agents can close one
                if (Calls(use.Target, type, agents))
                {
                    Report(context, Descriptors.AgentCircle, use, type.Name, use.Target.Name);
                }
            }

            ReportNamesOfferedTwice(context, agents[type], toolsets);
        }

        ReportClashesAcrossHandoffs(context, agents, ResolveTarget, [.. found.OwnTools]);

        ModuleAgent? ResolveTarget(INamedTypeSymbol agent) =>
            agents.TryGetValue(agent, out var own)
                ? Catalog.Summarize(Catalog.Resolve(own.Model, toolsets))
                : referenced[agent.ToTypeOf()].FirstOrDefault();
    }

    // Null for an agent that can be a tool or a handoff target
    private static string? GetUnusableReason(ModuleAgent agent) =>
        agent switch
        {
            { Described: false } => "it has neither a WithDescription nor a <summary>",
            _ when !ToolNames.IsLowerSnakeCase(agent.Name) => $"its name '{agent.Name}' is not lower_snake_case",
            _ => null
        };

    // D37, D57: an agent offers each tool name once, yet two toolsets it uses, of this assembly or a reference, may
    // share one, and so may a toolset and the agent's own tools
    private static void ReportNamesOfferedTwice(
        CompilationAnalysisContext context,
        FoundAgent agent,
        IReadOnlyList<ToolsetSummary> toolsets)
    {
        var offered = agent.Model.Toolsets
            .SelectMany(use => toolsets.Where(toolset => toolset.Type == use.Toolset)
                .Take(1)
                .SelectMany(toolset => Catalog.Offered(use, toolset)))
            .Select(tool => tool.Name)
            .Concat(agent.Model.Tools.Select(tool => tool.Name))
            .GroupBy(name => name)
            .Where(group => group.Count() > 1);

        foreach (var name in offered)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.OfferedTwice,
                agent.Type.Locations.FirstOrDefault(),
                $"The tool name '{name.Key}'"));
        }
    }

    // D13, D33, D57: a standing approval names its tool and reaches every agent a handoff passes the conversation to
    // and every sub-agent, so an agent's own tool shares no name with a tool of those; agents it never reaches may
    private static void ReportClashesAcrossHandoffs(
        CompilationAnalysisContext context,
        Dictionary<INamedTypeSymbol, FoundAgent> agents,
        System.Func<INamedTypeSymbol, ModuleAgent?> resolve,
        IReadOnlyList<(INamedTypeSymbol Agent, string Name, Location Location)> ownTools)
    {
        var reported = new HashSet<(Location, string)>();

        foreach (var from in agents.Keys)
        {
            if (resolve(from) is not { } source) continue;

            ReportDropped(from, source);

            foreach (var to in Reaches(from, agents))
            {
                if (resolve(to) is not { } target) continue;

                // A referenced agent's own tool has no location here, so its clash goes on the agent that reaches it
                Report(from, source, target, from.Locations.FirstOrDefault());
                Report(to, target, source, from.Locations.FirstOrDefault());
            }
        }

        // An own tool named like a tool of the agent's toolsets but for its underscores, which the agent leaves out;
        // the same name is TSH025, and two own tools of one agent the parser reports
        void ReportDropped(INamedTypeSymbol agent, ModuleAgent offered)
        {
            var own = ownTools.Where(tool => SymbolEqualityComparer.Default.Equals(tool.Agent, agent)).ToList();

            var dropped = own.Where(tool => !offered.Tools.Contains(tool.Name)
                && own.Count(other => Names.ToPascalCase(other.Name) == Names.ToPascalCase(tool.Name)) == 1);

            foreach (var tool in dropped)
            {
                Add(tool.Location, tool.Name);
            }
        }

        void Report(INamedTypeSymbol owner, ModuleAgent own, ModuleAgent other, Location? fallback)
        {
            var taken = new HashSet<string>(other.Tools.Select(Names.ToPascalCase));
            var otherOwn = new HashSet<string>(other.OwnTools.Select(Names.ToPascalCase));

            foreach (var name in own.OwnTools.Where(name => taken.Contains(Names.ToPascalCase(name))))
            {
                var declared = ownTools.FirstOrDefault(tool =>
                    SymbolEqualityComparer.Default.Equals(tool.Agent, owner) && tool.Name == name);

                // A clash with the other agent's own tool is reported there already, when that agent is this project's
                if (declared.Location is null && otherOwn.Contains(Names.ToPascalCase(name))) continue;

                Add(declared.Location ?? fallback ?? Location.None, name);
            }
        }

        void Add(Location location, string name)
        {
            if (!reported.Add((location, name))) return;

            context.ReportDiagnostic(
                Diagnostic.Create(Descriptors.DuplicateToolName, location, name, Names.ToPascalCase(name)));
        }
    }

    // The agents a standing approval of an agent reaches: a handoff passes the conversation on, and a sub-agent runs
    // with its caller's approvals but without a conversation, so it hands off nothing; a referenced agent's own uses
    // are not stamped
    private static IEnumerable<INamedTypeSymbol> Reaches(
        INamedTypeSymbol from,
        Dictionary<INamedTypeSymbol, FoundAgent> agents)
    {
        var reached = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { from };
        var handedOff = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { from };
        var called = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var next = new Stack<(INamedTypeSymbol Agent, bool AsSubAgent)>([(from, false)]);

        while (next.Count > 0)
        {
            var (agent, asSubAgent) = next.Pop();
            if (!agents.TryGetValue(agent, out var found)) continue;

            foreach (var use in found.Uses.Where(use => use.SubAgent || !asSubAgent))
            {
                var subAgent = use.SubAgent || asSubAgent;
                if (!(subAgent ? called : handedOff).Add(use.Target)) continue;

                next.Push((use.Target, subAgent));
                if (reached.Add(use.Target)) yield return use.Target;
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

        // The contexts the module registers, as the generator finds them
        if (type is { IsAbstract: true } or { IsGenericType: true }
            || !type.DerivesFrom(KnownTypes.JsonSerializerContext)
            || !type.IsVisibleInAssembly()) return;

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

        public ConcurrentBag<(INamedTypeSymbol Agent, string Name, Location Location)> OwnTools { get; } = [];

        public ConcurrentBag<ToolsetSummary> Toolsets { get; } = [];
    }

    private sealed record FoundAgent(INamedTypeSymbol Type, AgentModel Model, IReadOnlyList<AgentUseDeclaration> Uses);
}
