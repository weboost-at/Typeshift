using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Agentic.Generators.Parsing;

// Statement by statement; within a chain the outer call comes first, GetChainedCalls walks it from the start
internal static class ConstructorChains
{
    public static IEnumerable<IInvocationOperation> GetInvocations(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken ct)
    {
        foreach (var body in GetConstructors(type, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (compilation.GetSemanticModel(body.SyntaxTree).GetOperation(body, ct) is not { } operation) continue;

            foreach (var invocation in operation.Descendants().OfType<IInvocationOperation>())
            {
                yield return invocation;
            }
        }
    }

    public static IEnumerable<SyntaxNode> GetConstructors(INamedTypeSymbol type, CancellationToken ct) =>
        type.InstanceConstructors
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .SelectMany(constructor => constructor.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(ct));

    // AddTool(x).WithName(..).WithDescription(..): the calls made on the result of the given one, outwards
    public static IEnumerable<IInvocationOperation> GetChainedCalls(IInvocationOperation invocation)
    {
        IOperation current = invocation;

        while (current.Parent is IInvocationOperation outer && outer.Instance == current)
        {
            yield return outer;
            current = outer;
        }
    }

    // x.WithSettings<T>(..) reports at WithSettings<T>, not at the whole chain the invocation spans
    public static SyntaxNode GetCalledName(IInvocationOperation invocation) =>
        invocation.Syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name,
            InvocationExpressionSyntax { Expression: SimpleNameSyntax name } => name,
            _ => invocation.Syntax
        };

    public static IOperation Unwrap(IOperation operation) =>
        operation is IConversionOperation conversion ? Unwrap(conversion.Operand) : operation;

    public static string? GetConstantString(IInvocationOperation invocation) =>
        invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string text } ? text : null;

    // Configurable(s => s.Limit): the property read on the lambda's parameter
    public static IPropertySymbol? GetPickedProperty(IInvocationOperation configurable) =>
        configurable.Arguments[0].Value
            .DescendantsAndSelf()
            .OfType<IPropertyReferenceOperation>()
            .FirstOrDefault(reference => reference.Instance is IParameterReferenceOperation)
            ?.Property;
}
