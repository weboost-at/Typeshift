using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Tests.Repository.Support;

// The exceptions the library's public members throw, as far as their source shows it: a `throw new`, a call such
// as ArgumentNullException.ThrowIfNull, and what a member that is not public throws when the public one names it,
// in the same type. The built assembly has no bodies to read, so this reads the syntax trees of src/Typeshift
public static partial class ThrowingMembers
{
    private const string Library = "src/Typeshift";

    // One line for each exception a public member throws and its docs do not name
    public static IReadOnlyList<string> Undocumented { get; } =
    [
        .. RepositoryFiles.Find("*.cs", Library)
            .Select(file => CSharpSyntaxTree.ParseText(
                RepositoryFiles.Read(file),
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
                file))
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            .Where(type => !IsExtensionBlock(type) && IsVisible(type))
            .SelectMany(Gaps)
    ];

    private static IEnumerable<string> Gaps(TypeDeclarationSyntax type)
    {
        var members = type.Members.SelectMany(Flattened).ToList();

        var helpers = members.Where(member => !IsPublic(member, type))
            .SelectMany(member => Names(member).Select(name => (name, member)))
            .ToLookup(helper => helper.name, helper => helper.member);

        return
            from member in members
            where IsPublic(member, type) && !Docs(member).Contains("<inheritdoc", StringComparison.Ordinal)
            from thrown in Thrown(member, helpers, []).Distinct().Order(StringComparer.Ordinal)
            where !Documented(member).Contains(thrown)
            select $"{At(member)}: {Names(member).First()} throws {thrown}: say when in an <exception> tag";
    }

    // The exceptions the member's docs name, each by its type's own name
    private static IEnumerable<string> Documented(MemberDeclarationSyntax member) =>
        Tag().Matches(Docs(member)).Select(tag => tag.Groups["type"].Value);

    // An extension block's members are the static class's, as its helpers are theirs. Each stays the node of its
    // tree: a syntax list made of it would hold a copy without a file or a line
    private static IEnumerable<MemberDeclarationSyntax> Flattened(MemberDeclarationSyntax member) =>
        IsExtensionBlock(member) ? ((TypeDeclarationSyntax)member).Members : Enumerable.Repeat(member, 1);

    private static IEnumerable<string> Thrown(
        MemberDeclarationSyntax member,
        ILookup<string, MemberDeclarationSyntax> helpers,
        HashSet<MemberDeclarationSyntax> seen)
    {
        if (!seen.Add(member)) return [];

        // What a lambda throws, it throws when something calls it, which is not always the member
        var nodes = member.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax).ToList();

        var direct = nodes.Select(node => node switch
        {
            ThrowStatementSyntax { Expression: { } thrown } => Exception(thrown),
            ThrowExpressionSyntax { Expression: var thrown } => Exception(thrown),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax guard }
                when guard.Name.Identifier.Text.StartsWith("ThrowIf", StringComparison.Ordinal) =>
                ExceptionType(guard.Expression),
            _ => null
        }).OfType<string>();

        // A name the member reads on itself, never one it reads on something else
        var called = nodes.OfType<SimpleNameSyntax>()
            .Where(name => name.Parent is not MemberAccessExpressionSyntax access
                || access.Name != name
                || access.Expression is ThisExpressionSyntax)
            .SelectMany(name => helpers[name.Identifier.Text])
            .Distinct();

        return [.. direct, .. called.SelectMany(helper => Thrown(helper, helpers, seen))];
    }

    // The type of `new T(...)`, and of a factory on the exception's own type, such as T.For(...)
    private static string? Exception(ExpressionSyntax thrown) =>
        thrown switch
        {
            ObjectCreationExpressionSyntax creation => Unqualified(creation.Type),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax factory } =>
                ExceptionType(factory.Expression),
            _ => null
        };

    private static string? ExceptionType(ExpressionSyntax type) =>
        Unqualified(type) is { } name && name.EndsWith("Exception", StringComparison.Ordinal) ? name : null;

    private static string? Unqualified(ExpressionSyntax type) =>
        type switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
            SimpleNameSyntax simple => simple.Identifier.Text,
            _ => null
        };

    private static IEnumerable<string> Names(MemberDeclarationSyntax member) =>
        member switch
        {
            MethodDeclarationSyntax method => [method.Identifier.Text],
            PropertyDeclarationSyntax property => [property.Identifier.Text],
            ConstructorDeclarationSyntax constructor => [constructor.Identifier.Text],
            IndexerDeclarationSyntax => ["this[]"],
            OperatorDeclarationSyntax @operator => [$"operator {@operator.OperatorToken.Text}"],
            ConversionOperatorDeclarationSyntax conversion => [$"operator {conversion.Type}"],
            BaseFieldDeclarationSyntax fields => fields.Declaration.Variables.Select(field => field.Identifier.Text),
            _ => []
        };

    private static string Docs(MemberDeclarationSyntax member) =>
        string.Concat(member.GetLeadingTrivia().Where(trivia => trivia.HasStructure).Select(Text));

    private static string Text(SyntaxTrivia trivia) => trivia.ToFullString();

    private static bool IsPublic(MemberDeclarationSyntax member, TypeDeclarationSyntax type) =>
        member is not BaseTypeDeclarationSyntax
        && Names(member).Any()
        && (type is InterfaceDeclarationSyntax || IsVisible(member.Modifiers));

    private static bool IsVisible(TypeDeclarationSyntax type) =>
        type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().All(outer => IsVisible(outer.Modifiers));

    private static bool IsVisible(SyntaxTokenList modifiers) =>
        modifiers.Any(SyntaxKind.PublicKeyword)
        || (modifiers.Any(SyntaxKind.ProtectedKeyword) && !modifiers.Any(SyntaxKind.PrivateKeyword));

    private static bool IsExtensionBlock(SyntaxNode node) => node.IsKind(SyntaxKind.ExtensionBlockDeclaration);

    private static string At(SyntaxNode node) =>
        $"{node.SyntaxTree.FilePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    [GeneratedRegex(@"<exception cref=""(?:[\w.]+\.)?(?<type>\w+)[""{]")]
    private static partial Regex Tag();
}
