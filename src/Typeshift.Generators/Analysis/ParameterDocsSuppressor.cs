using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Typeshift.Generators.Analysis;

// D14: the parameters of a tool method the model never sees need no <param>, though the others have one
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParameterDocsSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor HiddenParameter = new(
        "TSHS001",
        "CS1573",
        "The model never sees this parameter of a tool, so it needs no <param>");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [HiddenParameter];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var ct = context.CancellationToken;

        // Read once, and only for a compilation that has such a warning at all
        var tools = new Lazy<Dictionary<ISymbol, HashSet<ITypeSymbol>>>(() => FindTools(context));

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree) continue;

            var node = tree.GetRoot(ct).FindNode(diagnostic.Location.SourceSpan);

            if (node.FirstAncestorOrSelf<ParameterSyntax>() is { } syntax
                && context.GetSemanticModel(tree).GetDeclaredSymbol(syntax, ct) is IParameterSymbol parameter
                && IsHidden(parameter, tools.Value))
            {
                context.ReportSuppression(Suppression.Create(HiddenParameter, diagnostic));
            }
        }
    }

    // What the call fills is told by the type, nullable or not, as the parser tells it. The method's class decides
    // nothing: a tool's method may live in any class (D23), and a toolset's other methods are no tools
    private static bool IsHidden(IParameterSymbol parameter, Dictionary<ISymbol, HashSet<ITypeSymbol>> tools)
    {
        if (!tools.TryGetValue(parameter.ContainingSymbol.OriginalDefinition, out var settings)) return false;

        var type = parameter.Type is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        } nullable
            ? nullable.TypeArguments[0]
            : parameter.Type;

        return type.Is(KnownTypes.ServiceProvider)
            || type.Is(KnownTypes.CancellationToken)
            || type.Is(KnownTypes.FunctionArguments)
            || settings.Contains(type);
    }

    // The method of every AddTool, each with the settings types its chains name. A suppressor gets no operations,
    // so the calls are found by their name and bound from syntax, as far as the code binds while it is typed
    private static Dictionary<ISymbol, HashSet<ITypeSymbol>> FindTools(SuppressionAnalysisContext context)
    {
        var ct = context.CancellationToken;
        var tools = new Dictionary<ISymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var tree in context.Compilation.SyntaxTrees.Where(tree => !Names.IsGenerated(tree)))
        {
            var addTools = tree.GetRoot(ct)
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(call => call.ArgumentList.Arguments.Count == 1 && GetName(call.Expression) == "AddTool")
                .ToList();

            if (addTools.Count == 0) continue;

            var model = context.GetSemanticModel(tree);

            foreach (var addTool in addTools)
            {
                // Another class's AddTool declares no tool
                if (!Binds(model.GetSymbolInfo(addTool, ct)).Any(ToolsetParser.IsAddTool)) continue;

                var settings = GetSettings(addTool)
                    .Select(type => model.GetTypeInfo(type, ct).Type)
                    .OfType<ITypeSymbol>()
                    .ToList();

                foreach (var method in Binds(model.GetSymbolInfo(GetMethod(addTool), ct)))
                {
                    if (!tools.TryGetValue(method.OriginalDefinition, out var named))
                    {
                        named = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                        tools[method.OriginalDefinition] = named;
                    }

                    named.UnionWith(settings);
                }
            }
        }

        return tools;
    }

    private static IEnumerable<IMethodSymbol> Binds(SymbolInfo info) =>
        info.Symbol is IMethodSymbol method ? [method] : info.CandidateSymbols.OfType<IMethodSymbol>();

    private static string? GetName(ExpressionSyntax called) =>
        called switch
        {
            SimpleNameSyntax name => name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null
        };

    // The method group AddTool names, under the cast that picks one of two overloads
    private static ExpressionSyntax GetMethod(InvocationExpressionSyntax addTool)
    {
        var argument = addTool.ArgumentList.Arguments[0].Expression;

        while (argument is CastExpressionSyntax or ParenthesizedExpressionSyntax)
        {
            argument = argument is CastExpressionSyntax cast
                ? cast.Expression
                : ((ParenthesizedExpressionSyntax)argument).Expression;
        }

        return argument;
    }

    // AddTool(x).WithDescription(..).WithSettings<T>(): the type arguments of the settings calls chained onto it
    private static IEnumerable<TypeSyntax> GetSettings(InvocationExpressionSyntax addTool)
    {
        SyntaxNode current = addTool;

        while (current.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax outer } access
            && access.Expression == current)
        {
            if (access.Name is GenericNameSyntax
                {
                    Identifier.ValueText: "WithSettings" or "WithConfigurableSettings",
                    TypeArgumentList.Arguments: { Count: 1 } arguments
                })
                yield return arguments[0];

            current = outer;
        }
    }
}
