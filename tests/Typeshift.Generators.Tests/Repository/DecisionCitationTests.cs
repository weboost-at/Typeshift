using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

public partial class DecisionCitationTests
{
    // Every decision a comment names, wherever it stands in the comment: code, project and build files, workflows
    // and hooks. The Markdown pages cite in prose, which the docs lint reads
    private static readonly IReadOnlyList<(string File, int Line, string Id)> Cited =
    [
        .. RepositoryFiles.Find("*", "src", "samples", "tests", "build", "demos", ".github", ".claude/hooks")
            .Concat(["Directory.Build.props", "Directory.Packages.props", ".editorconfig"])
            .SelectMany(Citations)
    ];

    [Fact]
    public void Every_Decision_A_Comment_Cites_Is_In_Force()
    {
        // Arrange
        var decisions = RepositoryFiles.Read("docs/contributing/decisions.md");
        var inForce = Ids(Heading(), decisions).Except(Ids(WithdrawnRow(), decisions)).ToHashSet();

        // Act
        var stale = Cited
            .Where(cited => !inForce.Contains(cited.Id))
            .Select(cited => $"{cited.File}:{cited.Line}: D{cited.Id}: not in force, cite the decision that is")
            .ToList();

        // Assert
        stale.Should().BeEmpty(Listing.Of(stale));
    }

    [Fact]
    public void The_Citations_Of_Every_Comment_Syntax_Are_Read()
    {
        // Act
        var read = Cited.Select(cited => Path.GetExtension(cited.File)).Distinct();

        // Assert
        read.Should().Contain([".cs", ".props", ".yml"], "a syntax the guard cannot read passes whatever it cites");
    }

    private static IEnumerable<(string File, int Line, string Id)> Citations(string file)
    {
        var text = RepositoryFiles.Read(file);

        return Comment()
            .Matches(text)
            .SelectMany(comment => Id()
                .Matches(comment.Value)
                .Select(id => (file, LineAt(text, comment.Index + id.Index), id.Groups["id"].Value)));
    }

    private static int LineAt(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<string> Ids(Regex pattern, string decisions) =>
        pattern.Matches(decisions).Select(match => match.Groups["id"].Value);

    [GeneratedRegex(@"^### D(?<id>\d+)\. ", RegexOptions.Multiline)]
    private static partial Regex Heading();

    // A withdrawn decision is a row of the withdrawn table, whether or not its heading is gone yet
    [GeneratedRegex(@"^\| D(?<id>\d+) \|", RegexOptions.Multiline)]
    private static partial Regex WithdrawnRow();

    // To the end of the line after // or a # that stands alone, and all between <!-- and -->, over several lines
    [GeneratedRegex(@"//[^\n]*|(?<!\S)#\s[^\n]*|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"\bD(?<id>\d+)\b")]
    private static partial Regex Id();
}
