using System.Threading;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

// D1, D75: the services.AddTypeshift() and TypeshiftRuntime.Create() calls of the library's methods, the ones the
// generator intercepts
internal static class InterceptParser
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken ct) =>
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
        && access.Name.Identifier.ValueText switch
        {
            KnownTypes.RegistrationMethod => true,
            KnownTypes.RuntimeMethod => NamesRuntime(access.Expression),
            _ => false
        };

    public static InterceptModel? Parse(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var info = context.SemanticModel.GetSymbolInfo(invocation, ct);

        // D75: a call bound at run time, such as one with a dynamic argument, reaches the body that says how to call it
        if (info.CandidateReason is CandidateReason.LateBound) return null;

        // The generator's own pass has none of its output yet, so a configure lambda that names some, an agent's
        // Settings for example, leaves a sole candidate
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
        if (symbol is not IMethodSymbol method) return null;

        var isRuntime = IsRuntimeMethod(method);
        if (!isRuntime && !IsLibraryMethod(method)) return null;

        return context.SemanticModel.GetInterceptableLocation(invocation, ct) is { } location
            ? new InterceptModel(location.GetInterceptsLocationAttributeSyntax(), isRuntime)
            : null;
    }

    // TypeshiftRuntime.Create, Typeshift.TypeshiftRuntime.Create or global::Typeshift.TypeshiftRuntime.Create
    private static bool NamesRuntime(ExpressionSyntax receiver) =>
        receiver is IdentifierNameSyntax { Identifier.ValueText: KnownTypes.RuntimeName }
            or MemberAccessExpressionSyntax { Name.Identifier.ValueText: KnownTypes.RuntimeName };

    // The calls the generator writes a replacement for
    public static bool IsIntercepted(IMethodSymbol method) => IsRuntimeMethod(method) || IsLibraryMethod(method);

    private static bool IsRuntimeMethod(IMethodSymbol method) =>
        method is { Name: KnownTypes.RuntimeMethod, IsStatic: true } && method.ContainingType.Is(KnownTypes.Runtime);

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
