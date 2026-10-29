using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree) continue;

            var node = tree.GetRoot(ct).FindNode(diagnostic.Location.SourceSpan);

            if (node.FirstAncestorOrSelf<ParameterSyntax>() is { } syntax
                && context.GetSemanticModel(tree).GetDeclaredSymbol(syntax, ct) is IParameterSymbol parameter
                && IsHidden(parameter, context.GetSemanticModel, ct))
            {
                context.ReportSuppression(Suppression.Create(HiddenParameter, diagnostic));
            }
        }
    }

    private static bool IsHidden(
        IParameterSymbol parameter,
        Func<SyntaxTree, SemanticModel> models,
        CancellationToken ct) =>
        parameter.ContainingSymbol is IMethodSymbol { IsStatic: true, ContainingType: { } owner }
        && (owner.DerivesFrom(KnownTypes.Toolset) || owner.DerivesFrom(KnownTypes.Agent))
        && (parameter.Type.Is(KnownTypes.ServiceProvider)
            || parameter.Type.Is(KnownTypes.CancellationToken)
            || parameter.Type.Is(KnownTypes.FunctionArguments)
            || IsSettingsOf(owner, parameter.Type, models, ct));

    // A type the toolset's or the agent's constructor names in WithSettings<T>() or WithConfigurableSettings<T>()
    private static bool IsSettingsOf(
        INamedTypeSymbol owner,
        ITypeSymbol type,
        Func<SyntaxTree, SemanticModel> models,
        CancellationToken ct) =>
        owner.InstanceConstructors
            .SelectMany(constructor => constructor.DeclaringSyntaxReferences)
            .SelectMany(reference => reference.GetSyntax(ct).DescendantNodes().OfType<GenericNameSyntax>())
            .Where(name => name.Identifier.ValueText is "WithSettings" or "WithConfigurableSettings")
            .Where(name => name.TypeArgumentList.Arguments.Count == 1)
            .Select(name => models(name.SyntaxTree).GetTypeInfo(name.TypeArgumentList.Arguments[0], ct).Type)
            .Any(settings => SymbolEqualityComparer.Default.Equals(settings, type));
}
