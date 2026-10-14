using Typeshift.Tests.BuiltIn.Support;
using Typeshift.Toolsets;
using FluentAssertions;

namespace Typeshift.Tests.BuiltIn;

public class ShellToolsetTests : WorkspaceTestBase
{
    [Fact]
    public async Task Given_A_Command_It_Runs_In_The_Root_And_Returns_Its_Exit_Code_And_Output()
    {
        // Act
        var result = await ShellToolset.RunCommandAsync("pwd; exit 3", new ShellSettings(), Scope, Ct);

        // Assert
        result.ExitCode.Should().Be(3);
        result.Output.Trim().Should().Be(new DirectoryInfo(Root).FullName);
    }

    [Fact]
    public async Task Given_A_Command_That_Outruns_Its_Timeout_It_Is_Killed()
    {
        // Arrange
        var settings = new ShellSettings { Timeout = TimeSpan.FromMilliseconds(200) };

        // Act
        var result = await ShellToolset.RunCommandAsync("sleep 30", settings, Scope, Ct);

        // Assert
        result.TimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task Given_Output_Over_The_Limit_The_Model_Receives_Its_Start_And_Its_End()
    {
        // Arrange
        var settings = new ShellSettings { MaxOutputLength = 20 };

        // Act
        var result = await ShellToolset.RunCommandAsync("seq 1 100", settings, Scope, Ct);

        // Assert
        result.Output.Should().StartWith("1\n2\n3\n4\n5\n");
        result.Output.Should().Contain("characters left out");
        result.Output.Should().EndWith("99\n100\n");
    }
}
