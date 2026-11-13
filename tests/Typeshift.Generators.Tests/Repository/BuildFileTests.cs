using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// What the workflows and the session hook must take from the build files instead of repeating it, and how the
// workflows name the code of others they run
public partial class BuildFileTests
{
    [Fact]
    public void The_Sdk_Version_Is_Named_Only_In_Global_Json()
    {
        // Act
        var files = RepositoryFiles.Find("*.yml", ".github/workflows")
            .Concat(RepositoryFiles.Find("*.sh", ".claude/hooks"))
            .Where(file => SdkVersion().IsMatch(RepositoryFiles.Read(file)));

        // Assert
        files.Should().BeEmpty("a workflow or a hook installs the SDK global.json asks for");
    }

    [Fact]
    public void The_Workflows_Name_Every_Action_By_Its_Commit()
    {
        // Act
        var actions = RepositoryFiles.Find("*.yml", ".github/workflows")
            .SelectMany(file => TaggedAction().Matches(RepositoryFiles.Read(file)))
            .Select(match => match.Value);

        // Assert
        actions.Should().BeEmpty("a tag can move to other code, and these jobs publish the package and the site");
    }

    // setup-dotnet's version input and the name of an SDK package in a distribution's feed
    [GeneratedRegex("dotnet-version:|dotnet-sdk-")]
    private static partial Regex SdkVersion();

    // An action that is not named by a full commit id; ./ names a workflow of this repository
    [GeneratedRegex(@"uses: (?!\./)\S+@(?![0-9a-f]{40}(\s|$))\S+")]
    private static partial Regex TaggedAction();
}
