using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Analysis;

// D2: every TSH diagnostic, at real locations, so #pragma and .editorconfig reach them
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeshiftAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Descriptors.NotPartial,
        Descriptors.NotConstant,
        Descriptors.NotStaticMethodGroup,
        Descriptors.UnboundParameter,
        Descriptors.SettingsNotRecord,
        Descriptors.SettingsLessAccessible,
        Descriptors.DuplicateToolName,
        Descriptors.NotLowerSnakeCase,
        Descriptors.MissingDescription,
        Descriptors.UnreadSettings,
        Descriptors.DocumentationOff,
        Descriptors.NotSettable,
        Descriptors.OutsideConstructor,
        Descriptors.UnreadableChain,
        Descriptors.Unreachable,
        Descriptors.NotDirectlyDerived,
        Descriptors.GeneratedNameClash
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(KnownTypes.Toolset) is null) return;

        var tools = new ConcurrentBag<(string Name, INamedTypeSymbol Toolset, Location Location)>();

        context.RegisterSymbolStartAction(start => StartToolset(start, tools), SymbolKind.NamedType);
        context.RegisterOperationAction(AnalyzeCall, OperationKind.Invocation);
        context.RegisterCompilationEndAction(end => ReportClashesAcrossToolsets(end, tools));
    }

    private static void StartToolset(
        SymbolStartAnalysisContext context,
        ConcurrentBag<(string Name, INamedTypeSymbol Toolset, Location Location)> tools)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsAbstract || !type.DerivesFrom(KnownTypes.Toolset)) return;

        var constructors = new ConcurrentBag<IOperation>();

        context.RegisterOperationBlockAction(block =>
        {
            if (block.OwningSymbol is not IMethodSymbol { MethodKind: MethodKind.Constructor }) return;

            foreach (var operation in block.OperationBlocks)
            {
                constructors.Add(operation);
            }
        });

        context.RegisterSymbolEndAction(end =>
        {
            var reporter = new Reporter(end.ReportDiagnostic);

            if (GetIndirection(type) is { } indirection)
            {
                reporter.Report(
                    Descriptors.NotDirectlyDerived,
                    type.Locations.FirstOrDefault(),
                    type.Name,
                    indirection,
                    "Toolset");
                return;
            }

            var documented = type.DeclaringSyntaxReferences
                .Any(reference => reference.SyntaxTree.Options.DocumentationMode != DocumentationMode.None);

            // Constructors come in no particular order; the source order decides which of two clashing tools is
            // reported
            var ordered = constructors
                .OrderBy(operation => operation.Syntax.SyntaxTree.FilePath)
                .ThenBy(operation => operation.Syntax.SpanStart);

            ToolsetParser.Parse(type, ordered, documented, reporter, end.CancellationToken, out var declarations);

            foreach (var (tool, addTool) in declarations)
            {
                tools.Add((tool.Name, type, addTool.Syntax.GetLocation()));
            }
        });
    }

    private static string? GetIndirection(INamedTypeSymbol type) =>
        type switch
        {
            { IsGenericType: true } => "is generic",
            { BaseType: var baseType } when !baseType.Is(KnownTypes.Toolset) => $"derives from '{baseType!.Name}'",
            _ => null
        };

    private static void AnalyzeCall(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var target = invocation.TargetMethod;

        if (ToolsetParser.IsAddTool(invocation) && !IsDirectlyInConstructor(invocation, context.ContainingSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.OutsideConstructor,
                ConstructorChains.GetCalledName(invocation).GetLocation(),
                target.Name));

            return;
        }

        // D4: a chain call on a variable goes on a chain the generator never sees
        if (IsChainCall(target) && ConstructorChains.Unwrap(invocation.Instance!) is not IInvocationOperation)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.UnreadableChain,
                invocation.Syntax.GetLocation(),
                "tool chain"));
        }
    }

    private static bool IsChainCall(IMethodSymbol target) =>
        !target.IsStatic
        && (target.ContainingType.Is(KnownTypes.ToolBuilder)
            || target.ContainingType.Is(KnownTypes.SettingsBuilder)
            || target.ContainingType.Is(KnownTypes.ConfigurableSettingsBuilder));

    private static bool IsDirectlyInConstructor(IOperation operation, ISymbol containing)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) return false;
        }

        return containing is IMethodSymbol { MethodKind: MethodKind.Constructor };
    }

    // D13: a tool name is unique across everything the host sees; within one toolset the parser reports it
    private static void ReportClashesAcrossToolsets(
        CompilationAnalysisContext context,
        IEnumerable<(string Name, INamedTypeSymbol Toolset, Location Location)> tools)
    {
        var ordered = tools
            .OrderBy(tool => tool.Location.SourceTree?.FilePath)
            .ThenBy(tool => tool.Location.SourceSpan.Start);

        foreach (var group in ordered.GroupBy(tool => Names.ToPascalCase(tool.Name)))
        {
            var first = group.First();

            var clashes = group
                .Skip(1)
                .Where(tool => !SymbolEqualityComparer.Default.Equals(tool.Toolset, first.Toolset));

            foreach (var clash in clashes)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Descriptors.DuplicateToolName, clash.Location, clash.Name, group.Key));
            }
        }
    }
}
