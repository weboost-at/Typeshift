using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Tests.Repository;

// A reader searches the guide's list of run-time errors for the words of a message, so each quote there is a message
// of the library or of the code the generator writes, and each exception the library declares has a row
public partial class RunTimeErrorTests
{
    private const StringComparison Ordinal = StringComparison.Ordinal;

    // What a message fills in: a value in the code, a word in braces in the docs
    private const string Hole = "\0";

    private const string Docs = "docs/guide/diagnostics.md";

    private const string Heading = "\n## Errors at run time\n";

    // The rows of the section's tables, without their headers
    private static readonly IReadOnlyList<string> Rows =
    [
        .. GetSection(RepositoryFiles.Read(Docs))
            .Split('\n')
            .Where(line => line.StartsWith("| ", Ordinal) && !line.StartsWith("| Exception |", Ordinal))
    ];

    // Every text the code writes, its literals joined where it adds them up
    private static readonly IReadOnlyList<string> Messages =
    [
        .. RepositoryFiles.Find("*.cs", "src/Typeshift", "src/Typeshift.Generators/Emitting")
            .Select(file => CSharpSyntaxTree.ParseText(
                RepositoryFiles.Read(file),
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
                file))
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
            .Where(expression => IsText(expression) && !IsPart(expression))
            .Select(GetText)
    ];

    [Fact]
    public void Every_Message_The_Docs_Quote_Is_Written_By_The_Code()
    {
        // Act
        var violations = Rows
            .SelectMany(row => Quote().Matches(row.Split(" | ")[1]).Select(quote => quote.Groups["text"].Value))
            .Where(quote => !Messages.Any(message => message.Contains(Placeholder().Replace(quote, Hole), Ordinal)))
            .Select(quote => $"{Docs}: `{quote}` is in no message of the code: quote the message's own words")
            .ToList();

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    [Fact]
    public void Every_Exception_The_Library_Declares_Has_A_Row()
    {
        // Act
        var violations = typeof(Agent).Assembly.GetExportedTypes()
            .Where(type => type.IsAssignableTo(typeof(Exception)))
            .Where(type => !Rows.Any(row => row.StartsWith($"| `{type.Name}` |", Ordinal)))
            .Select(type => $"{Docs}: {type.Name} has no row: add one under Errors at run time")
            .ToList();

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    private static string GetSection(string docs)
    {
        var start = docs.IndexOf(Heading, Ordinal);
        if (start < 0) throw new InvalidOperationException($"{Docs} has no section Errors at run time.");

        var end = docs.IndexOf("\n## ", start + Heading.Length, Ordinal);
        return docs[start..(end < 0 ? docs.Length : end)];
    }

    private static bool IsText(ExpressionSyntax expression) =>
        expression switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
            InterpolatedStringExpressionSyntax => true,
            BinaryExpressionSyntax sum =>
                sum.IsKind(SyntaxKind.AddExpression) && (IsText(sum.Left) || IsText(sum.Right)),
            ParenthesizedExpressionSyntax inner => IsText(inner.Expression),
            _ => false
        };

    // A literal added to others, which counts as the text of the sum
    private static bool IsPart(ExpressionSyntax expression) =>
        expression.Parent is BinaryExpressionSyntax or ParenthesizedExpressionSyntax
        && IsText((ExpressionSyntax)expression.Parent);

    private static string GetText(ExpressionSyntax expression) =>
        expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated =>
                string.Concat(interpolated.Contents.Select(part =>
                    part is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : Hole)),
            BinaryExpressionSyntax sum when IsText(sum) => GetText(sum.Left) + GetText(sum.Right),
            ParenthesizedExpressionSyntax inner when IsText(inner) => GetText(inner.Expression),
            _ => Hole
        };

    // The quotes of a row's message, each as code
    [GeneratedRegex("`(?<text>[^`]+)`")]
    private static partial Regex Quote();

    // A value the message fills in, such as {agent}
    [GeneratedRegex(@"\{[^{}]+\}")]
    private static partial Regex Placeholder();
}
