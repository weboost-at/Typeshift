using System.Threading;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Agentic.Generators.Parsing;

// Finds the services.AddAgentic() calls of the library's one public method, the ones the host intercepts
internal static class InterceptParser
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken ct) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AddAgentic" }
        };

    public static InterceptModel? Parse(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method) return null;
        if (!IsLibraryMethod(method)) return null;

        if (context.SemanticModel.GetInterceptableLocation(invocation, ct) is not { } location) return null;

        return new InterceptModel(
            location.GetInterceptsLocationAttributeSyntax(),
            LocationInfo.From(invocation.GetLocation()));
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
