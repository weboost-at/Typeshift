using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Tests.Repository;

// The code style rules of AGENTS.md a check can decide, for the library, the generator, the samples and the tests.
// A rule about code reads the syntax tree, so a string or a comment that names the pattern passes and an uncommon
// spelling does not; a violation reads "<file>:<line>: <what to do>". The clock is banned by symbol in the build
// (BannedSymbols.txt)
public partial class SourceConventionTests
{
    private static readonly string[] Code = ["src", "samples", "tests"];

    // What only generated code calls is hidden from editors (D10), so its docs may name the decision behind a member
    private const string HiddenFromEditors = "src/Typeshift/Generated/";

    // The templates of generated code, which compiles in a consumer's build, where no analyzer of this repository runs
    private const string Emitters = "src/Typeshift.Generators/";

    private static readonly IReadOnlyList<SyntaxTree> Trees =
    [
        .. RepositoryFiles.Find("*.cs", Code).Select(file => CSharpSyntaxTree.ParseText(
            RepositoryFiles.Read(file),
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            file))
    ];

    [Fact]
    public void The_Code_The_Generator_Writes_Never_Reads_The_Clock()
    {
        // Act
        var violations = Texts(Clock(), "generated code takes the TimeProvider of the services")
            .Where(line => line.StartsWith(Emitters, StringComparison.Ordinal))
            .ToList();

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void The_Code_Compares_With_Null_Through_Patterns()
    {
        // Act
        var violations = Nodes<ExpressionSyntax>(ComparesWithNull, "write is null or is not null");

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void Extension_Members_Are_Declared_In_Extension_Blocks()
    {
        // Act
        var violations = Nodes<ParameterSyntax>(
            parameter => parameter.Modifiers.Any(SyntaxKind.ThisKeyword),
            "declare the member in an extension(...) block");

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void The_Code_Fits_120_Columns()
    {
        // Act
        var violations = Lines(LongLine(), "wrap the line at 120 columns");

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void The_Code_Holds_No_Todo()
    {
        // Act
        var violations = Comments(Marker(), "an open item goes to docs/contributing/status.md")
            .Concat(Nodes<ObjectCreationExpressionSyntax>(
                creation => Unqualified(creation.Type) == "NotImplementedException",
                "implement it, or throw NotSupportedException for what will never work"))
            .ToList();

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void Given_Xml_Docs_They_Cite_No_Decision()
    {
        // Act
        var violations = Lines(DocumentedDecision(), "a user reads the docs in a tooltip, say it without the number")
            .Where(line => !line.StartsWith(HiddenFromEditors, StringComparison.Ordinal))
            .ToList();

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void Given_Xml_Docs_They_Never_End_An_Example_With_Say()
    {
        // Act
        var violations = Lines(TrailingSay(), "an example says so with 'for example' or 'such as'");

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    private static List<string> Nodes<T>(Func<T, bool> breaks, string hint)
        where T : SyntaxNode =>
    [
        .. Trees.SelectMany(tree => tree.GetRoot()
            .DescendantNodes()
            .OfType<T>()
            .Where(breaks)
            .Select(node => At(tree, node.SpanStart, hint)))
    ];

    // Each match in a comment, whether it has a line, trails a statement, fills a block or documents a member
    private static IEnumerable<string> Comments(Regex rule, string hint) =>
        Trees.SelectMany(tree => tree.GetRoot()
            .DescendantTrivia()
            .Where(trivia => trivia.Kind() is SyntaxKind.SingleLineCommentTrivia
                or SyntaxKind.MultiLineCommentTrivia
                or SyntaxKind.SingleLineDocumentationCommentTrivia
                or SyntaxKind.MultiLineDocumentationCommentTrivia)
            .SelectMany(trivia => rule
                .Matches(trivia.ToFullString())
                .Select(match => At(tree, trivia.FullSpan.Start + match.Index, hint))));

    // Each match in the text of a string, of whatever kind
    private static IEnumerable<string> Texts(Regex rule, string hint) =>
        Trees.SelectMany(tree => tree.GetRoot()
            .DescendantTokens()
            .Where(token => token.Kind() is SyntaxKind.StringLiteralToken
                or SyntaxKind.InterpolatedStringTextToken
                or SyntaxKind.SingleLineRawStringLiteralToken
                or SyntaxKind.MultiLineRawStringLiteralToken)
            .SelectMany(token => rule
                .Matches(token.Text)
                .Select(match => At(tree, token.SpanStart + match.Index, hint))));

    private static List<string> Lines(Regex rule, string hint) =>
    [
        .. Trees.SelectMany(tree => tree.GetText()
            .Lines
            .Where(line => rule.IsMatch(line.ToString()))
            .Select(line => At(tree, line.Start, hint)))
    ];

    private static string At(SyntaxTree tree, int position, string hint) =>
        $"{tree.FilePath}:{tree.GetText().Lines.GetLineFromPosition(position).LineNumber + 1}: {hint}";

    private static bool ComparesWithNull(ExpressionSyntax expression) =>
        expression switch
        {
            BinaryExpressionSyntax comparison =>
                comparison.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
                && (IsNull(comparison.Left) || IsNull(comparison.Right))
                && !IsInExpressionTree(comparison),
            InvocationExpressionSyntax call =>
                Unqualified(call.Expression) == "ReferenceEquals"
                && call.ArgumentList.Arguments.Any(argument => IsNull(argument.Expression)),
            _ => false
        };

    private static bool IsNull(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    // An expression tree takes no pattern (CS8122), so == null stays in a lambda that is declared as one
    private static bool IsInExpressionTree(SyntaxNode node) =>
        node.Ancestors().OfType<LambdaExpressionSyntax>().LastOrDefault()?.Parent switch
        {
            CastExpressionSyntax cast => IsExpression(cast.Type),
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax local } } =>
                IsExpression(local.Type),
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => IsExpression(property.Type),
            ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } => IsExpression(property.Type),
            ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax method } => IsExpression(method.ReturnType),
            _ => false
        };

    private static bool IsExpression(TypeSyntax type) =>
        Unqualified(type).StartsWith("Expression<", StringComparison.Ordinal);

    // The name without what qualifies it: a namespace, global:: or the type a static member is called on
    private static string Unqualified(SyntaxNode name) =>
        name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.ToString(),
            AliasQualifiedNameSyntax aliased => aliased.Name.ToString(),
            MemberAccessExpressionSyntax member => member.Name.ToString(),
            _ => name.ToString()
        };

    // What reads or waits on the real clock, as an emitter would write it into generated code
    [GeneratedRegex(
        @"\bDateTime(Offset)?\.(UtcNow|Now|Today)\b|\bStopwatch\b|\bEnvironment\.TickCount|\bTimeProvider\.System\b"
        + @"|\bTask\.Delay\(|\bThread\.Sleep\(|\.CancelAfter\(")]
    private static partial Regex Clock();

    // A citation in parentheses or as a prefix, in a doc comment or in one an emitter writes
    [GeneratedRegex(@"///.*(\(D\d+(, D\d+)*\)|\bD\d+:)")]
    private static partial Regex DocumentedDecision();

    [GeneratedRegex(@"^.{121}")]
    private static partial Regex LongLine();

    // In capitals, as the marker is written: a sample may be about a todo list
    [GeneratedRegex(@"\b(TODO|FIXME|HACK)\b")]
    private static partial Regex Marker();

    [GeneratedRegex(@"///.*\bsay[,;.)]")]
    private static partial Regex TrailingSay();
}
