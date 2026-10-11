using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Agentic.Generators.Parsing;

// Reads the fluent calls in a class's constructors, in source order
internal static class ConstructorChains
{
    public static IEnumerable<IInvocationOperation> GetInvocations(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken ct)
    {
        var bodies = type.InstanceConstructors
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .SelectMany(constructor => constructor.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(ct));

        foreach (var body in bodies)
        {
            ct.ThrowIfCancellationRequested();

            if (compilation.GetSemanticModel(body.SyntaxTree).GetOperation(body, ct) is not { } operation) continue;

            foreach (var invocation in operation.Descendants().OfType<IInvocationOperation>())
            {
                yield return invocation;
            }
        }
    }

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

    // picker => picker.Configurable(s => s.A, s => s.B): every property read on a picked lambda's parameter
    public static IEnumerable<string> GetPickedProperties(IInvocationOperation invocation, string pickerType) =>
        invocation.Arguments
            .SelectMany(argument => argument.Value.DescendantsAndSelf())
            .OfType<IInvocationOperation>()
            .Where(pick => pick.TargetMethod.Name == "Configurable" && pick.TargetMethod.ContainingType.Is(pickerType))
            .SelectMany(pick => pick.Arguments.SelectMany(argument => argument.Value.DescendantsAndSelf()))
            .OfType<IPropertyReferenceOperation>()
            .Where(reference => reference.Instance is IParameterReferenceOperation)
            .OrderBy(reference => reference.Syntax.SpanStart)
            .Select(reference => reference.Property.Name)
            .Distinct();
}
