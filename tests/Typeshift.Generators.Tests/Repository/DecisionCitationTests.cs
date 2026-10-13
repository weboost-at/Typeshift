using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

public partial class DecisionCitationTests
{
    [Fact]
    public void Every_Decision_The_Code_Cites_Is_In_Force()
    {
        // Arrange
        var decisions = RepositoryFiles.Read("docs/contributing/decisions.md");
        var inForce = Ids(Heading(), decisions).Except(Ids(WithdrawnRow(), decisions)).ToHashSet();

        // Act
        var stale = RepositoryFiles.Find("*.*", "src", "samples", "tests")
            .SelectMany(file => File.ReadLines(Path.Combine(RepositoryFiles.Root, file))
                .Select((line, index) => (Line: $"{file}:{index + 1}", Citation: Citation().Match(line))))
            .Where(cited => cited.Citation.Success && !inForce.Contains(cited.Citation.Groups["id"].Value))
            .Select(cited => cited.Line);

        // Assert
        stale.Should().BeEmpty();
    }

    private static IEnumerable<string> Ids(Regex pattern, string decisions) =>
        pattern.Matches(decisions).Select(match => match.Groups["id"].Value);

    [GeneratedRegex(@"^### D(?<id>\d+)\. ", RegexOptions.Multiline)]
    private static partial Regex Heading();

    // A withdrawn decision is a row of the withdrawn table, whether or not its heading is gone yet
    [GeneratedRegex(@"^\| D(?<id>\d+) \|", RegexOptions.Multiline)]
    private static partial Regex WithdrawnRow();

    [GeneratedRegex(@"(//|<!--)\s*D(?<id>\d+):")]
    private static partial Regex Citation();
}
