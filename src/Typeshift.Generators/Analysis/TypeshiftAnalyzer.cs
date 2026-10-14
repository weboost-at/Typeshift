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
        Descriptors.NotStorable,
        Descriptors.NotSettable,
        Descriptors.OutsideConstructor,
        Descriptors.UnreadableChain,
        Descriptors.Unreachable,
        Descriptors.NotDirectlyDerived,
        Descriptors.GeneratedNameClash,
        Descriptors.NotGenerated,
        Descriptors.UnstampedReference,
        Descriptors.OfferedTwice
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

        context.RegisterSymbolStartAction(start => StartClass(start, tools), SymbolKind.NamedType);
        context.RegisterOperationAction(AnalyzeCall, OperationKind.Invocation);

        context.RegisterCompilationEndAction(end =>
        {
            ReportClashesAcrossToolsets(end, tools);
            ReportUnstampedReferences(end);
        });
    }

    private static void StartClass(
        SymbolStartAnalysisContext context,
        ConcurrentBag<(string Name, INamedTypeSymbol Toolset, Location Location)> tools)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsAbstract) return;

        var baseType = type.DerivesFrom(KnownTypes.Toolset) ? "Toolset"
            : type.DerivesFrom(KnownTypes.Agent) ? "Agent"
            : null;

        if (baseType is null) return;

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

            if (GetIndirection(type, baseType) is { } indirection)
            {
                reporter.Report(
                    Descriptors.NotDirectlyDerived,
                    type.Locations.FirstOrDefault(),
                    type.Name,
                    indirection,
                    baseType);
                return;
            }

            // Constructors come in no particular order; the source order decides which of two clashing
            // declarations is reported
            var ordered = constructors
                .OrderBy(operation => operation.Syntax.SyntaxTree.FilePath)
                .ThenBy(operation => operation.Syntax.SpanStart)
                .ToList();

            if (baseType == "Agent")
            {
                AgentParser.Parse(type, ordered, reporter, end.CancellationToken);
                return;
            }

            var documented = type.DeclaringSyntaxReferences
                .Any(reference => reference.SyntaxTree.Options.DocumentationMode != DocumentationMode.None);

            ToolsetParser.Parse(type, ordered, documented, reporter, end.CancellationToken, out var declarations);

            foreach (var (tool, addTool) in declarations)
            {
                tools.Add((tool.Name, type, addTool.Syntax.GetLocation()));
            }
        });
    }

    private static string? GetIndirection(INamedTypeSymbol type, string baseType) =>
        type switch
        {
            { IsGenericType: true } => "is generic",
            { BaseType: var direct } when !direct.Is($"Typeshift.{baseType}") => $"derives from '{direct!.Name}'",
            _ => null
        };

    private static void AnalyzeCall(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var target = invocation.TargetMethod;

        var declares = ToolsetParser.IsAddTool(invocation) || AgentParser.IsDeclaration(invocation);

        if (declares && !IsDirectlyInConstructor(invocation, context.ContainingSymbol))
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
                "declaration chain"));
        }
    }

    private static readonly string[] Builders =
    [
        KnownTypes.ToolBuilder,
        KnownTypes.SettingsBuilder,
        KnownTypes.ConfigurableSettingsBuilder,
        KnownTypes.ToolsetUse,
        KnownTypes.ToolInclusion,
        KnownTypes.ToolExclusion,
        KnownTypes.ChatOptionsBuilder,
        KnownTypes.RunOptionsBuilder
    ];

    private static bool IsChainCall(IMethodSymbol target) =>
        !target.IsStatic && Builders.Any(builder => target.ContainingType.Is(builder));

    private static bool IsDirectlyInConstructor(IOperation operation, ISymbol containing)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) return false;
        }

        return containing is IMethodSymbol { MethodKind: MethodKind.Constructor };
    }

    // D13: a tool name is unique across everything the host sees, the tools the references stamp too; within one
    // toolset the parser reports it
    private static void ReportClashesAcrossToolsets(
        CompilationAnalysisContext context,
        IEnumerable<(string Name, INamedTypeSymbol Toolset, Location Location)> tools)
    {
        var referenced = new HashSet<string>(ReferenceReader.Read(context.Compilation, context.CancellationToken)
            .Toolsets
            .SelectMany(toolset => toolset.Tools)
            .Select(tool => Names.ToPascalCase(tool.Name)));

        var ordered = tools
            .OrderBy(tool => tool.Location.SourceTree?.FilePath)
            .ThenBy(tool => tool.Location.SourceSpan.Start);

        foreach (var group in ordered.GroupBy(tool => Names.ToPascalCase(tool.Name)))
        {
            var first = group.First();

            var clashes = referenced.Contains(group.Key)
                ? group
                : group.Skip(1).Where(tool => !SymbolEqualityComparer.Default.Equals(tool.Toolset, first.Toolset));

            foreach (var clash in clashes)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Descriptors.DuplicateToolName, clash.Location, clash.Name, group.Key));
            }
        }
    }

    // D9: a reference whose toolsets and agents AddTypeshift() cannot register
    private static void ReportUnstampedReferences(CompilationAnalysisContext context)
    {
        foreach (var assembly in ReferenceReader.GetUnstamped(context.Compilation, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.UnstampedReference, Location.None, assembly.Name));
        }
    }
}
