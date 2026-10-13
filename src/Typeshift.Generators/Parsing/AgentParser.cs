using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Parsing;

// Toolsets stay names here and a later step resolves them, so a toolset edit reaches the agents using it
internal static class AgentParser
{
    public static AgentModel Parse(INamedTypeSymbol agent, Compilation compilation, CancellationToken ct)
    {
        var diagnostics = new DiagnosticBag();
        var notPartial = agent.GetNotPartial(ct);
        var isPartial = notPartial is null;

        if (notPartial is not null)
            diagnostics.Report(Descriptors.NotPartial, notPartial.Locations.FirstOrDefault(), notPartial.Name);

        var invocations = ConstructorChains.GetInvocations(agent, compilation, ct).ToList();

        var options = invocations
            .Where(call => call.TargetMethod.Name == "Configurable"
                && call.TargetMethod.ContainingType.Is(KnownTypes.ChatOptionsBuilder))
            .OrderBy(call => ConstructorChains.GetCalledName(call).SpanStart)
            .Select(call => ReadConfigurableOption(call, diagnostics))
            .OfType<ChatOptionModel>()
            .GroupBy(option => option.Name)
            .Select(group => group.First());

        return new AgentModel(
            Type: TypeModels.From(agent),
            IsPartial: isPartial,
            ConfigurableOptions: options.ToEquatableArray(),
            Uses: invocations
                .Where(call => call.TargetMethod.Name == "UseToolset"
                    && call.TargetMethod.ContainingType.Is(KnownTypes.AgentDefinition))
                .Select(ReadUse)
                .ToEquatableArray(),
            Diagnostics: diagnostics.ToEquatableArray());
    }

    private static ChatOptionModel? ReadConfigurableOption(IInvocationOperation call, DiagnosticBag diagnostics)
    {
        if (ConstructorChains.GetPickedProperty(call) is not { } property) return null;

        if (Storage.Of(property.Type) is { } storage)
            return new ChatOptionModel(property.Name, property.Type.ToNullableFullyQualified(), storage);

        diagnostics.Report(
            Descriptors.UnsupportedConfigurableType,
            ConstructorChains.GetCalledName(call),
            property.Name,
            property.Type.ToDisplayString());

        return null;
    }

    // D29: the tool properties are generated, so in the generator's own view they do not bind: the Include and
    // Exclude calls chained after UseToolset<T>() are read from their syntax
    private static ToolsetUseModel ReadUse(IInvocationOperation call)
    {
        var toolset = call.TargetMethod.TypeArguments[0];
        var picks = GetPicks((ExpressionSyntax)call.Syntax).ToList();

        return new ToolsetUseModel(
            Toolset: toolset.ToTypeOf(),
            IsPublic: toolset.IsEffectivelyPublic(),
            Excludes: picks.Any(pick => pick.Method == "Exclude"),
            Picked: picks.Select(pick => pick.Tool).OfType<string>().ToEquatableArray(),
            Location: LocationInfo.From(ConstructorChains.GetCalledName(call).GetLocation()));
    }

    // UseToolset<T>().Include(t => t.A).Include(t => t.B): each method chained on the result and the property its
    // lambda reads, outwards
    private static IEnumerable<(string Method, string? Tool)> GetPicks(ExpressionSyntax call)
    {
        for (var current = call;
             current.Parent is MemberAccessExpressionSyntax access && access.Parent is InvocationExpressionSyntax outer;
             current = outer)
        {
            var tool = outer.ArgumentList.Arguments.FirstOrDefault()?.Expression is LambdaExpressionSyntax
            {
                ExpressionBody: MemberAccessExpressionSyntax property
            }
                ? property.Name.Identifier.ValueText
                : null;

            yield return (access.Name.Identifier.ValueText, tool);
        }
    }
}
