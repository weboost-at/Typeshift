using System.Threading;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

// D1, D75: the services.AddTypeshift() and TypeshiftApp.Create() calls of the library's methods, the ones the
// generator intercepts
internal static class InterceptParser
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken ct) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: KnownTypes.RegistrationMethod or KnownTypes.AppMethod
            }
        };

    public static InterceptModel? Parse(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // The generator's own pass has no aliases yet, so a configure lambda that names one leaves a sole candidate
        var info = context.SemanticModel.GetSymbolInfo(invocation, ct);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
        if (symbol is not IMethodSymbol method) return null;

        var isApp = method is { Name: KnownTypes.AppMethod, IsStatic: true }
            && method.ContainingType.Is(KnownTypes.App);
        if (!isApp && !IsLibraryMethod(method)) return null;

        return context.SemanticModel.GetInterceptableLocation(invocation, ct) is { } location
            ? new InterceptModel(location.GetInterceptsLocationAttributeSyntax(), isApp)
            : null;
    }

    // A call of a C# 14 extension member binds to a method of the extension block nested in the declaring class
    private static bool IsLibraryMethod(IMethodSymbol method)
    {
        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.Is(KnownTypes.ServiceCollectionExtensions)) return true;
        }

        return false;
    }
}
