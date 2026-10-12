using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agentic.Generators.Diagnostics;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Agentic.Generators.Parsing;

// Toolsets stay names here and a later step resolves them, so a toolset edit reaches the agents using it
internal static class AgentParser
{
    public static AgentModel Parse(INamedTypeSymbol agent, Compilation compilation, CancellationToken ct)
    {
        var diagnostics = new DiagnosticBag();
        var isPartial = agent.IsPartial(ct);
        var options = new List<ChatOptionModel>();

        if (!isPartial) diagnostics.Report(Descriptors.NotPartial, agent.Locations.FirstOrDefault(), agent.Name);

        var calls = ConstructorChains.GetInvocations(agent, compilation, ct)
            .Where(invocation => invocation.TargetMethod.ContainingType.Is(KnownTypes.AgentDefinition));

        foreach (var call in calls)
        {
            switch (call.TargetMethod.Name)
            {
                case "WithChatOptions":
                    options.AddRange(ReadConfigurableOptions(call, compilation, diagnostics));
                    break;

            }
        }

        return new AgentModel(
            Type: TypeModels.From(agent),
            IsPartial: isPartial,
            ConfigurableOptions: options.ToEquatableArray(),
            Uses: ReadUses(agent, compilation, ct).ToEquatableArray(),
            Diagnostics: diagnostics.ToEquatableArray());
    }

    private static IEnumerable<ChatOptionModel> ReadConfigurableOptions(
        IInvocationOperation call,
        Compilation compilation,
        DiagnosticBag diagnostics)
    {
        var chatOptions = compilation.GetTypeByMetadataName(KnownTypes.ChatOptions);

        var picked = ConstructorChains.GetPickedProperties(call, KnownTypes.ConfigurableSettingsPicker)
            .Select(name => chatOptions?.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault())
            .OfType<IPropertySymbol>();

        foreach (var property in picked)
        {
            if (Storage.Of(property.Type) is { } storage)
            {
                yield return new ChatOptionModel(property.Name, property.Type.ToNullableFullyQualified(), storage);
                continue;
            }

            diagnostics.Report(
                Descriptors.UnsupportedConfigurableType,
                ConstructorChains.GetCalledName(call),
                property.Name,
                property.Type.ToDisplayString());
        }
    }

    // The selection methods are generated, so in the generator's own view a selection does not bind: the call is read
    // from its syntax, and its target from the overload candidates
    private static IEnumerable<ToolsetUseModel> ReadUses(
        INamedTypeSymbol agent,
        Compilation compilation,
        CancellationToken ct)
    {
        foreach (var constructor in ConstructorChains.GetConstructors(agent, ct))
        {
            var model = compilation.GetSemanticModel(constructor.SyntaxTree);

            foreach (var call in constructor.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (GetUseToolset(call) is not { } name || !IsUseToolset(model.GetSymbolInfo(call, ct))) continue;
                if (model.GetTypeInfo(name.TypeArgumentList.Arguments[0], ct).Type is not { } toolset) continue;

                var arguments = call.ArgumentList.Arguments;

                yield return new ToolsetUseModel(
                    Toolset: toolset.ToTypeOf(),
                    IsPublic: toolset.IsEffectivelyPublic(),
                    All: arguments.Count == 0,
                    Selected: arguments.Count == 0 ? [] : GetSelectionCalls(arguments[0].Expression).ToEquatableArray(),
                    Location: LocationInfo.From(name.GetLocation()));
            }
        }
    }

    private static GenericNameSyntax? GetUseToolset(InvocationExpressionSyntax call) =>
        call.Expression switch
        {
            GenericNameSyntax { Identifier.ValueText: "UseToolset" } name => name,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "UseToolset" } name } => name,
            _ => null
        };

    private static bool IsUseToolset(SymbolInfo symbol) =>
        (symbol.Symbol is null ? symbol.CandidateSymbols : [symbol.Symbol])
        .Any(candidate => candidate.ContainingType.Is(KnownTypes.AgentDefinition));

    // tools => tools.A().B(), or a block of such statements, read in source order
    private static IEnumerable<string> GetSelectionCalls(SyntaxNode select) =>
        select switch
        {
            LambdaExpressionSyntax { ExpressionBody: { } body } => GetChainedNames(body),
            LambdaExpressionSyntax { Block: { } block } => block.Statements
                .OfType<ExpressionStatementSyntax>()
                .SelectMany(statement => GetChainedNames(statement.Expression)),
            _ => []
        };

    private static IEnumerable<string> GetChainedNames(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            ? [.. GetChainedNames(access.Expression), access.Name.Identifier.ValueText]
            : [];
}
