using System.Text.RegularExpressions;
using Agentic.Tests.Testing.Conventions;
using FluentAssertions;

namespace Agentic.Tests.Documentation;

public partial class ReadmeTests
{
    private static readonly string RepositoryDirectory = Path.Combine(TestSources.TestsDirectory, "..");

    [Fact]
    public void Every_Quickstart_Block_Is_The_Code_Of_The_Sample_File_It_Names()
    {
        // Arrange
        var readme = File.ReadAllText(Path.Combine(RepositoryDirectory, "README.md"));

        // Act
        var blocks = SampleBlock().Matches(readme).ToList();
        var stale = blocks.Where(block => block.Groups["code"].Value != Sample(block.Groups["path"].Value));

        // Assert
        blocks.Should().NotBeEmpty();
        stale.Select(block => block.Groups["path"].Value).Should().BeEmpty();
    }

    private static string Sample(string path) => File.ReadAllText(Path.Combine(RepositoryDirectory, path));

    // A block the README copies from the sample: <!-- sample: path --> right above a csharp fence
    [GeneratedRegex(@"<!-- sample: (?<path>\S+) -->\n```csharp\n(?<code>.*?)```", RegexOptions.Singleline)]
    private static partial Regex SampleBlock();
}
