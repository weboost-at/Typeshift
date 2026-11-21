using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// A renamed or removed test leaves its snapshot behind, which nothing compares any more
public partial class SnapshotFileTests
{
    private const string Project = "tests/Typeshift.Generators.Tests";

    private const string Suffix = ".verified.txt";

    [Fact]
    public void Every_Verified_Snapshot_Belongs_To_A_Test()
    {
        // Arrange
        var ofTests = typeof(SnapshotFileTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods().Select(method => $"{type.Name}.{method.Name}"));

        var named = RepositoryFiles.Find("*.cs", Project)
            .SelectMany(file => NamedSnapshot().Matches(RepositoryFiles.Read(file)))
            .Select(match => match.Groups["name"].Value);

        var owned = ofTests.Concat(named).ToHashSet();

        // Act
        var orphans = RepositoryFiles.Find($"*{Suffix}", $"{Project}/Snapshots")
            .Select(file => Path.GetFileName(file)[..^Suffix.Length])
            .Where(snapshot => !owned.Contains(snapshot));

        // Assert
        orphans.Should().BeEmpty();
    }

    // A snapshot of the repository's own, verified under the name a test passes
    [GeneratedRegex(@"Snapshot\.Verify\(""(?<name>[^""]+)""")]
    private static partial Regex NamedSnapshot();
}
