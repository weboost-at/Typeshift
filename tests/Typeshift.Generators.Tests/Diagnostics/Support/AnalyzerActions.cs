using Typeshift.Generators.Tests.Repository.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Tests.Diagnostics.Support;

// Which fields of Descriptors the analyzer reports from which of its actions, read from its source. An editor runs
// the actions of a class and of a call while the code is typed, and the end of the compilation on a build. A parser
// reports to the action of the class it reads
public static class AnalyzerActions
{
    private const string Analyzer = "src/Typeshift.Generators/Analysis/TypeshiftAnalyzer.cs";

    private static readonly CSharpParseOptions Options =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static readonly SyntaxNode Root = Parse(Analyzer);

    private static readonly ILookup<string, MethodDeclarationSyntax> Methods = Root.DescendantNodes()
        .OfType<MethodDeclarationSyntax>()
        .ToLookup(method => method.Identifier.ValueText);

    private static readonly SyntaxNode EndAction = Root.DescendantNodes()
        .OfType<InvocationExpressionSyntax>()
        .Single(call => call.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "RegisterCompilationEndAction"
        })
        .ArgumentList;

    public static IReadOnlyCollection<string> AtTheEnd { get; } = Reported([EndAction]);

    public static IReadOnlyCollection<string> WhileTyping { get; } =
    [
        .. Reported(Methods["Initialize"]),
        .. RepositoryFiles.Find("*.cs", "src/Typeshift.Generators/Parsing").SelectMany(file => Descriptors(Parse(file)))
    ];

    // The descriptors named by the nodes and by every method of the analyzer they reach, the end action left out
    // unless it is where the search starts
    private static HashSet<string> Reported(IEnumerable<SyntaxNode> roots)
    {
        var reached = new HashSet<SyntaxNode>(roots);
        var next = new Queue<SyntaxNode>(reached);

        while (next.Count > 0)
        {
            // A method of the analyzer is called or passed by its name alone; x.Start is a member of something else
            var called = Within(next.Dequeue())
                .OfType<IdentifierNameSyntax>()
                .Where(name => name.Parent is not MemberAccessExpressionSyntax access || access.Name != name)
                .SelectMany(name => Methods[name.Identifier.ValueText]);

            foreach (var method in called.Where(reached.Add))
            {
                next.Enqueue(method);
            }
        }

        return [.. reached.SelectMany(Descriptors)];
    }

    private static IEnumerable<string> Descriptors(SyntaxNode node) =>
        Within(node)
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax { Identifier.ValueText: "Descriptors" })
            .Select(access => access.Name.Identifier.ValueText);

    private static IEnumerable<SyntaxNode> Within(SyntaxNode node) =>
        node.DescendantNodes(child => child == node || child != EndAction);

    private static SyntaxNode Parse(string file) =>
        CSharpSyntaxTree.ParseText(RepositoryFiles.Read(file), Options).GetRoot();
}
