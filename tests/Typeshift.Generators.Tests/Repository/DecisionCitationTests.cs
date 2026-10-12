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
        var inForce = InForce().Matches(RepositoryFiles.Read("docs/contributing/decisions.md"))
            .Select(match => match.Groups["id"].Value)
            .ToHashSet();

        // Act
        var stale = RepositoryFiles.Find("*.*", "src", "samples", "tests")
            .SelectMany(file => File.ReadLines(Path.Combine(RepositoryFiles.Root, file))
                .Select((line, index) => (Line: $"{file}:{index + 1}", Citation: Citation().Match(line))))
            .Where(cited => cited.Citation.Success && !inForce.Contains(cited.Citation.Groups["id"].Value))
            .Select(cited => cited.Line);

        // Assert
        stale.Should().BeEmpty();
    }

    [GeneratedRegex(@"^### D(?<id>\d+)\. (?!Withdrawn)", RegexOptions.Multiline)]
    private static partial Regex InForce();

    [GeneratedRegex(@"(//|<!--)\s*D(?<id>\d+):")]
    private static partial Regex Citation();
}
