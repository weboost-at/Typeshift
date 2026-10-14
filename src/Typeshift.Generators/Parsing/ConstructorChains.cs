using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Parsing;

// Statement by statement; within a chain the outer call comes first, GetChainedCalls walks it from the start
internal static class ConstructorChains
{
    // The generator's own way to the bodies; the analyzer hands over the operation blocks it is given
    public static IReadOnlyList<IOperation> GetBlocks(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken ct) =>
    [
        .. type.InstanceConstructors
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .SelectMany(constructor => constructor.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(ct))
            .Select(syntax => compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax, ct))
            .OfType<IOperation>()
    ];

    // D4: only calls made directly in a constructor count; TSH014 reports those in lambdas and local functions
    public static IEnumerable<IInvocationOperation> GetInvocations(IEnumerable<IOperation> blocks) =>
        blocks.SelectMany(Walk).OfType<IInvocationOperation>();

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

    // Like GetChainedCalls, but through the calls that pick from a tool list: in the generator they do not bind, the
    // list's properties being its own output, so they are invalid operations there
    public static IEnumerable<IOperation> GetChain(IOperation start)
    {
        var current = start;

        while (current.Parent is { } outer && IsCalledOn(outer, current))
        {
            yield return outer;
            current = outer;
        }
    }

    // A chain the generator reads ends in a statement of its own; one stored or passed on can go on elsewhere
    public static bool EndsInStatement(IInvocationOperation invocation) =>
        (GetChain(invocation).LastOrDefault() ?? invocation).Parent is IExpressionStatementOperation;

    // D4: a declaration under an if or in a loop would run only sometimes, the generated code always
    public static bool IsTopLevelStatement(IInvocationOperation invocation) =>
        (GetChain(invocation).LastOrDefault() ?? invocation).Parent is IExpressionStatementOperation
        {
            Parent: IBlockOperation { Parent: null or IConstructorBodyOperation or IMethodBodyBaseOperation }
        };

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

    // s => s.Limit: the property the lambda reads from its parameter, and nothing else; null for any other shape
    public static IPropertySymbol? GetPickedProperty(IInvocationOperation pick) =>
        Unwrap(pick.Arguments[0].Value) switch
        {
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation lambda } => GetPickedProperty(lambda),
            IAnonymousFunctionOperation lambda => GetPickedProperty(lambda),
            _ => null
        };

    private static IPropertySymbol? GetPickedProperty(IAnonymousFunctionOperation lambda) =>
        lambda.Body.Operations.Length == 1
        && lambda.Body.Operations[0] is IReturnOperation { ReturnedValue: { } value }
        && Unwrap(value) is IPropertyReferenceOperation { Instance: IParameterReferenceOperation } reference
            ? reference.Property
            : null;

    private static IEnumerable<IOperation> Walk(IOperation operation)
    {
        yield return operation;

        foreach (var child in operation.ChildOperations)
        {
            if (child is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;

            foreach (var descendant in Walk(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsCalledOn(IOperation outer, IOperation receiver) =>
        outer switch
        {
            IInvocationOperation invocation => invocation.Instance == receiver,
            IInvalidOperation => outer.Syntax is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax access
            }
            && access.Expression == receiver.Syntax,
            _ => false
        };
}
