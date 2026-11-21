using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    // D4: the module creates a toolset or an agent with new T(), which runs the parameterless constructor and those
    // it chains to with this(...), so only their blocks declare, and a declaration in any other is TSH044. A class
    // without a parameterless constructor is TSH016 already, and keeps every block
    public static IReadOnlyList<IOperation> GetRun(
        INamedTypeSymbol type,
        IEnumerable<IOperation> blocks,
        Reporter reporter)
    {
        var all = blocks.ToList();
        var constructor = type.InstanceConstructors.FirstOrDefault(each => each.Parameters.IsEmpty);
        if (constructor is null || all.Count == 0) return all;

        var owners = all.Select(block => GetConstructor(type, block.Syntax)).ToList();
        var run = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        while (constructor is not null && run.Add(constructor))
        {
            constructor = all
                .Where((_, index) => SymbolEqualityComparer.Default.Equals(owners[index], constructor))
                .Select(block => GetChained(type, block))
                .FirstOrDefault(chained => chained is not null);
        }

        var read = new List<IOperation>();

        for (var index = 0; index < all.Count; index++)
        {
            if (owners[index] is not { } owner || run.Contains(owner))
            {
                read.Add(all[index]);
                continue;
            }

            foreach (var call in GetInvocations([all[index]]).Where(IsDeclaration))
            {
                reporter.Report(Descriptors.NeverRun, GetCalledName(call), call.TargetMethod.Name, type.Name);
            }
        }

        return read;
    }

    // A call of what Toolset and Agent offer a constructor: AddTool, WithDisplayName, UseToolset<T>() and the rest
    public static bool IsDeclaration(IInvocationOperation call) =>
        call.TargetMethod is { MethodKind: MethodKind.Ordinary, IsStatic: false, Name: not "GetInstructions" } method
        && (method.ContainingType.Is(KnownTypes.Toolset) || method.ContainingType.Is(KnownTypes.Agent));

    // The constructor whose declaration holds the syntax, the innermost where a primary constructor's declaration,
    // the whole class, holds the others too
    public static IMethodSymbol? GetConstructor(INamedTypeSymbol type, SyntaxNode syntax) =>
        type.InstanceConstructors
            .SelectMany(constructor => constructor.DeclaringSyntaxReferences
                .Where(reference => reference.SyntaxTree == syntax.SyntaxTree && reference.Span.Contains(syntax.Span))
                .Select(reference => (Constructor: constructor, reference.Span.Length)))
            .OrderBy(candidate => candidate.Length)
            .Select(candidate => candidate.Constructor)
            .FirstOrDefault();

    // The constructor of the same class that this(...) names; a block's initializer is its only place
    private static IMethodSymbol? GetChained(INamedTypeSymbol type, IOperation block)
    {
        var initializer = block is IConstructorBodyOperation body ? body.Initializer : block;
        if (initializer is null) return null;

        return GetInvocations([initializer])
            .FirstOrDefault(call => call.Syntax is ConstructorInitializerSyntax
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, type))
            ?.TargetMethod;
    }

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

    // WithName("..."): the text a call's first argument holds, or TSH002 and null for one that is no constant
    public static string? ReadConstant(IInvocationOperation call, string what, Reporter reporter)
    {
        if (call.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string text }) return text;

        reporter.Report(Descriptors.NotConstant, call.Arguments[0].Syntax, what);
        return null;
    }

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
            && Strip(access.Expression) == receiver.Syntax,
            _ => false
        };

    // (x) and x! are syntax without an operation of their own, so the receiver's operation is that of x
    private static ExpressionSyntax Strip(ExpressionSyntax expression) =>
        expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => Strip(parenthesized.Expression),
            PostfixUnaryExpressionSyntax suppressed when suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression)
                => Strip(suppressed.Operand),
            _ => expression
        };
}
