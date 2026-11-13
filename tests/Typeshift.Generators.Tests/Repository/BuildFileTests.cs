using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// What the workflows and the session hook must take from the build files instead of repeating it
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

    // setup-dotnet's version input and the name of an SDK package in a distribution's feed
    [GeneratedRegex("dotnet-version:|dotnet-sdk-")]
    private static partial Regex SdkVersion();
}
