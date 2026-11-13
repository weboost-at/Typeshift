using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Parsing;

internal static class AgentParser
{
    // D6: the members the generator adds to every agent, which no member of the user's may share
    public static readonly string[] Members = ["Settings", "Declaration", "TypeshiftGenerated", "_typeshift"];

    // Null for an agent the generator cannot extend; its declarations are still parsed, so every mistake is reported
    public static AgentModel? Parse(
        INamedTypeSymbol agent,
        IEnumerable<IOperation> constructors,
        bool documented,
        Reporter reporter,
        CancellationToken ct) =>
        Parse(agent, constructors, documented, reporter, ct, out _, out _);

    // The analyzer also needs the agents an agent uses, to check them against each other (D33, D34), and each of its
    // own tools' declaration, to report names that clash across the host
    public static AgentModel? Parse(
        INamedTypeSymbol agent,
        IEnumerable<IOperation> constructors,
        bool documented,
        Reporter reporter,
        CancellationToken ct,
        out IReadOnlyList<AgentUseDeclaration> agents,
        out IReadOnlyList<(ToolModel Tool, IInvocationOperation AddTool)> tools)
    {
        var notPartial = agent.GetNotPartial(ct);

        if (notPartial is not null)
            reporter.Report(Descriptors.NotPartial, notPartial.Locations.FirstOrDefault(), notPartial.Name);

        var unreachable = agent.GetUnreachableReason();

        if (unreachable is not null)
            reporter.Report(Descriptors.Unreachable, agent.Locations.FirstOrDefault(), agent.Name, unreachable);

        var clash = Members.SelectMany(name => agent.GetMembers(name)).FirstOrDefault(IsUsers);

        if (clash is not null)
            reporter.Report(Descriptors.GeneratedNameClash, clash.Locations.FirstOrDefault(), clash.Name, agent.Name);

        var declarations = new Declarations();

        foreach (var call in ConstructorChains.GetInvocations(constructors).Where(IsDeclaration))
        {
            if (!ConstructorChains.EndsInStatement(call))
            {
                reporter.Report(Descriptors.UnreadableChain, call.Syntax, "declaration chain");
                continue;
            }

            if (!ConstructorChains.IsTopLevelStatement(call))
            {
                var name = ConstructorChains.GetCalledName(call);
                reporter.Report(Descriptors.OutsideConstructor, name, call.TargetMethod.Name);
                continue;
            }

            Read(agent, call, declarations, reporter);
        }

        agents = declarations.Agents;
        tools = ToolsetParser.ParseTools(agent, constructors, documented, reporter, ct);

        // D14: as a tool's, an agent's summary describes it unless WithDescription says otherwise; without generated
        // docs it is lost
        var summary = XmlDocs.Read(agent, ct).Summary;
        var description = declarations.Description ?? (string.IsNullOrEmpty(summary) ? null : summary);

        if (description is null && !documented && XmlDocs.ReadUnparsed(agent, ct).Length > 0)
        {
            reporter.Report(
                Descriptors.DocumentationOff,
                agent.Locations.FirstOrDefault(),
                $"The agent '{agent.Name}'");
        }

        var providerId = ReadProviderId(agent, constructors, declarations, reporter);

        // A partial in another file would declare another class than a file-local one
        if (notPartial is not null || clash is not null || agent.IsFileLocalOrNestedInOne()) return null;

        // The user's own Configure{Tool}() wins over the one the generator would write
        var configures = agent.GetMembers()
            .Where(member => member.Name.StartsWith("Configure", StringComparison.Ordinal) && IsUsers(member))
            .Select(member => member.Name)
            .Distinct();

        ISymbol[] named = [agent, .. declarations.Used, .. declarations.Models.Select(model => model.Type)];

        return new AgentModel(
            TypeModels.From(agent),
            unreachable is null,
            declarations.Name ?? ToolNames.ToSnakeCase(agent.Name),
            description,
            declarations.Kind ?? agent.Name,
            declarations.SchemaVersion ?? 1,
            providerId,
            declarations.ChatOptions.ToEquatableArray(),
            declarations.RunOptions.ToEquatableArray(),
            declarations.Toolsets.ToEquatableArray(),
            tools.Select(declaration => declaration.Tool).ToEquatableArray(),
            configures.ToEquatableArray(),
            named.GetExperimentalIds());
    }

    // AddTool declares a tool, which ToolsetParser reads in an agent as in a toolset
    public static bool IsDeclaration(IInvocationOperation invocation) =>
        invocation.TargetMethod is { IsStatic: false, Name: not ("GetInstructions" or "AddTool") } method
        && method.ContainingType.Is(KnownTypes.Agent);

    // The analyzer sees the generated members too; only the user's own can clash
    private static bool IsUsers(ISymbol member) =>
        member.DeclaringSyntaxReferences.Any(reference => !Names.IsGenerated(reference.SyntaxTree));

    private static void Read(
        INamedTypeSymbol agent,
        IInvocationOperation call,
        Declarations declarations,
        Reporter reporter)
    {
        switch (call.TargetMethod.Name)
        {
            case "WithName":
                declarations.Name = ReadConstant(call, "agent name", reporter) ?? declarations.Name;
                break;

            case "WithDescription":
                declarations.Description = ReadConstant(call, "agent description", reporter)
                    ?? declarations.Description;
                break;

            // D39, D40
            case "WithStorageKind":
                declarations.Kind = ReadConstant(call, "storage kind", reporter) ?? declarations.Kind;
                break;

            case "WithSchemaVersion":
                if (call.Arguments[0].Value.ConstantValue is { HasValue: true, Value: int version })
                    declarations.SchemaVersion = version;
                else
                    reporter.Report(Descriptors.NotConstant, call.Arguments[0].Syntax, "schema version");

                break;

            case "WithChatOptions":
                ReadModel(call, declarations);
                ReadPicks(call, OptionMembers.Chat, declarations.ChatOptions, declarations.ProviderPicks, reporter);
                break;

            case "WithRunOptions":
                ReadPicks(call, OptionMembers.Run, declarations.RunOptions, declarations.ProviderPicks, reporter);
                break;

            case "UseToolset":
                if (ReadUse(agent, call, declarations.Toolsets, reporter) is not { } use) break;

                declarations.Toolsets.Add(use);
                declarations.Used.Add(call.TargetMethod.TypeArguments[0]);
                break;

            // D33, D34
            case "UseAgent" or "UseHandoff":
                if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol { TypeKind: not TypeKind.Error } target)
                    break;

                var subAgent = call.TargetMethod.Name == "UseAgent";
                var syntax = ConstructorChains.GetCalledName(call);

                if (declarations.Agents.Any(other => other.SubAgent == subAgent && Same(other.Target, target)))
                    reporter.Report(Descriptors.OfferedTwice, syntax, $"'{target.Name}'");
                else
                    declarations.Agents.Add(new AgentUseDeclaration(target, subAgent, syntax));

                break;
        }
    }

    private static string? ReadConstant(IInvocationOperation call, string what, Reporter reporter)
    {
        if (ConstructorChains.GetConstantString(call) is { } text) return text;

        reporter.Report(Descriptors.NotConstant, call.Arguments[0].Syntax, what);
        return null;
    }

    // D77: the static type of WithModel's provider is the agent's provider id type
    private static void ReadModel(IInvocationOperation call, Declarations declarations)
    {
        var models = ConstructorChains.GetChainedCalls(call).Where(next => next.TargetMethod.Name == "WithModel");

        foreach (var model in models)
        {
            if (model.TargetMethod.TypeArguments[0] is not { TypeKind: not TypeKind.Error } type) continue;

            var argument = model.Arguments.First(passed => passed.Parameter?.Ordinal == 0).Syntax;
            declarations.Models.Add((type, argument));
        }
    }

    // D5: WithChatOptions().WithTemperature(0.3f).Configurable(o => o.Temperature)
    private static void ReadPicks(
        IInvocationOperation call,
        OptionMember[] options,
        List<string> picked,
        List<SyntaxNode> providerPicks,
        Reporter reporter)
    {
        var picks = ConstructorChains.GetChainedCalls(call).Where(next => next.TargetMethod.Name == "Configurable");

        foreach (var pick in picks)
        {
            if (ConstructorChains.GetPickedProperty(pick) is not { } property)
            {
                reporter.Report(Descriptors.UnreadableChain, pick.Arguments[0].Syntax, "option pick");
                continue;
            }

            var option = options.FirstOrDefault(member => member.Option == property.Name);

            if (option is null)
            {
                reporter.Report(Descriptors.NotStorable, pick.Arguments[0].Syntax, property.Name);
                continue;
            }

            if (!picked.Contains(property.Name)) picked.Add(property.Name);

            // D77: a configurable model brings the member of its provider
            if (option.Provider is not null) providerPicks.Add(pick.Arguments[0].Syntax);
        }
    }

    private static ToolsetUseModel? ReadUse(
        INamedTypeSymbol agent,
        IInvocationOperation call,
        IReadOnlyList<ToolsetUseModel> uses,
        Reporter reporter)
    {
        var toolset = call.TargetMethod.TypeArguments[0];

        // A type that does not resolve has no assembly to look a module up in; the compiler reports it
        if (toolset.TypeKind == TypeKind.Error) return null;

        var type = toolset.ToTypeOf();
        var name = ConstructorChains.GetCalledName(call);

        if (!IsGenerated(toolset))
        {
            reporter.Report(Descriptors.NotGenerated, name, toolset.Name);
            return null;
        }

        if (uses.Any(use => use.Toolset == type))
        {
            reporter.Report(Descriptors.OfferedTwice, name, $"'{toolset.Name}'");
            return null;
        }

        var picks = new List<string>();
        var excludes = false;

        // D4: the tool list's properties are the generator's own output, so the picks are read from syntax
        foreach (var pick in ConstructorChains.GetChain(call).Select(link => (InvocationExpressionSyntax)link.Syntax))
        {
            var argument = pick.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            var method = ((MemberAccessExpressionSyntax)pick.Expression).Name;
            excludes = method.Identifier.ValueText == "Exclude";

            // A pick still being typed has no argument yet
            if (argument is null || GetPickedName(argument) is not { } picked)
            {
                reporter.Report(Descriptors.UnreadableChain, (SyntaxNode?)argument ?? method, "tool pick");
                return null;
            }

            if (picks.Contains(picked))
            {
                reporter.Report(Descriptors.OfferedTwice, argument, $"'{picked}'");
                continue;
            }

            picks.Add(picked);
        }

        // D6: the agent's Settings record names the overrides of what it offers; the stamps on the toolset say which
        // tools have overrides, and the generator leaves out those the record cannot name
        var hidden = agent.IsEffectivelyPublic() && !toolset.IsEffectivelyPublic();

        if (hidden && ReferenceReader.GetOverridden(toolset).Any(property => Offers(excludes, picks, property)))
            reporter.Report(Descriptors.SettingsLessAccessible, name, toolset.Name, agent.Name);

        return new ToolsetUseModel(type, excludes, picks.ToEquatableArray());
    }

    // D77: the agent's provider id type, from the WithModel that runs last, string without one. Settings that store
    // a provider need a type they store as one value (TSH029) and can name (TSH006, D6), with string in its place
    // so the generated code stays valid; a public agent's Settings need a public one, or name it internal (TSH006)
    private static ProviderIdModel ReadProviderId(
        INamedTypeSymbol agent,
        IEnumerable<IOperation> constructors,
        Declarations declarations,
        Reporter reporter)
    {
        if (GetRunModel(agent, constructors, declarations.Models) is not var (type, argument))
            return ProviderIdModel.String;

        var stored = declarations.ProviderPicks.Count > 0;

        if (GetOneValue(type) is not { } storage)
        {
            if (stored) reporter.Report(Descriptors.UnusableProviderIdType, argument, type.ToDisplayString());
            return ProviderIdModel.String;
        }

        var nameable = IsNameable(type);
        var providerId = new ProviderIdModel(type.ToTypeOf(), storage, type.IsEffectivelyPublic());
        if (nameable && (providerId.IsPublic || !agent.IsEffectivelyPublic())) return providerId;

        foreach (var pick in declarations.ProviderPicks)
        {
            reporter.Report(Descriptors.SettingsLessAccessible, pick, type.Name, agent.Name);
        }

        return nameable ? providerId : ProviderIdModel.String;
    }

    // D77: the WithModel that new TAgent() runs last: the parameterless constructor runs those it chains to with
    // this(...) first, so the last WithModel of the outermost constructor that has one
    private static (ITypeSymbol Type, SyntaxNode Argument)? GetRunModel(
        INamedTypeSymbol agent,
        IEnumerable<IOperation> constructors,
        IReadOnlyList<(ITypeSymbol Type, SyntaxNode Argument)> models)
    {
        var chained = new Dictionary<IMethodSymbol, IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var call in ConstructorChains.GetInvocations(constructors))
        {
            if (call.Syntax is ConstructorInitializerSyntax
                && Same(call.TargetMethod.ContainingType, agent)
                && GetConstructor(agent, call.Syntax) is { } caller)
                chained[caller] = call.TargetMethod;
        }

        var constructor = agent.InstanceConstructors.FirstOrDefault(each => each.Parameters.IsEmpty);
        var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        while (constructor is not null && visited.Add(constructor))
        {
            var own = models.Where(model => Declares(GetConstructor(agent, model.Argument), constructor)).ToList();

            if (own.Count > 0) return own[own.Count - 1];

            constructor = chained.TryGetValue(constructor, out var next) ? next : null;
        }

        return null;
    }

    private static bool Declares(IMethodSymbol? declaring, IMethodSymbol constructor) =>
        SymbolEqualityComparer.Default.Equals(declaring, constructor);

    private static IMethodSymbol? GetConstructor(INamedTypeSymbol agent, SyntaxNode syntax) =>
        agent.InstanceConstructors.FirstOrDefault(constructor => constructor.DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree == syntax.SyntaxTree && reference.Span.Contains(syntax.Span)));

    // The generated Settings, in a file of their own, can name it
    private static bool IsNameable(ITypeSymbol type) =>
        type is not INamedTypeSymbol named || named.IsVisibleInAssembly();

    // A string, a number, a Guid or an enum; a nullable value type would type the code's provider and the stored
    // one apart
    private static StorageModel? GetOneValue(ITypeSymbol type) =>
        type is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
        && Storage.Of(type) is { Kind: StorageKind.Scalar or StorageKind.Number or StorageKind.Enum } storage
            ? storage
            : null;

    public static bool Offers(bool excludes, IReadOnlyCollection<string> picks, string property) =>
        picks.Count == 0 || picks.Contains(property) != excludes;

    // t => t.SearchTickets
    private static string? GetPickedName(SyntaxNode syntax) =>
        syntax is SimpleLambdaExpressionSyntax
        {
            Parameter.Identifier.ValueText: var parameter,
            ExpressionBody: MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: var receiver },
                Name: IdentifierNameSyntax { Identifier.ValueText: var property }
            }
        }
        && receiver == parameter
            ? property
            : null;

    // D7: a toolset of this compilation, or of an assembly that carries a module of this generator's stamps
    private static bool IsGenerated(ITypeSymbol toolset) =>
        toolset.Locations.Any(location => location.IsInSource)
        || ReferenceReader.GetModule(toolset.ContainingAssembly) is not null;

    // What the declarations of one constructor say, before they are checked
    private sealed class Declarations
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Kind { get; set; }

        public int? SchemaVersion { get; set; }

        public List<string> ChatOptions { get; } = [];

        public List<string> RunOptions { get; } = [];

        public List<SyntaxNode> ProviderPicks { get; } = [];

        // Each WithModel's provider: its static type and the argument, in source order
        public List<(ITypeSymbol Type, SyntaxNode Argument)> Models { get; } = [];

        public List<ToolsetUseModel> Toolsets { get; } = [];

        // The toolsets the uses name, which the generated code names too
        public List<ITypeSymbol> Used { get; } = [];

        public List<AgentUseDeclaration> Agents { get; } = [];
    }

    private static bool Same(ISymbol one, ISymbol other) => SymbolEqualityComparer.Default.Equals(one, other);
}

// An agent another one offers as a sub-agent, or hands off to
internal sealed record AgentUseDeclaration(INamedTypeSymbol Target, bool SubAgent, SyntaxNode Syntax);
