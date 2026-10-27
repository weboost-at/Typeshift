using Typeshift.Tests.BuiltIn.Support;
using Typeshift.Toolsets;
using FluentAssertions;

namespace Typeshift.Tests.BuiltIn;

public class ShellToolsetTests : WorkspaceTestBase
{
    // More than a pipe holds, so a command past it knows the reader took what it wrote before
    private const string Filler = "head -c 200000 /dev/zero | tr '\\0' x";

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
        var settings = new ShellSettings();

        // Act
        var result = await RunPastTheTimeoutAsync("sleep 30", settings);

        // Assert
        result.TimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Command_That_Leaves_A_Process_Behind_It_Returns_At_The_Timeout()
    {
        // Arrange
        var settings = new ShellSettings();
        var run = ShellToolset.RunCommandAsync($"echo started; {Filler}; sleep 30 & touch ready", settings, Scope, Ct);

        await WaitForFileAsync("ready", run);

        // Act
        Time.Advance(settings.Timeout + ShellToolset.DrainTime);
        var result = await run;

        // Assert
        result.Output.Should().StartWith("started\n");
    }

    [Fact]
    public async Task Given_A_Process_Left_Behind_What_It_Writes_Just_After_The_Timeout_Is_Kept()
    {
        // Arrange
        var settings = new ShellSettings();
        var late = $"(while [ ! -f go ]; do sleep 0.01; done; echo late; {Filler}; touch written; sleep 30)";
        var run = ShellToolset.RunCommandAsync($"{late} & touch ready", settings, Scope, Ct);

        await WaitForFileAsync("ready", run);
        Time.Advance(settings.Timeout);

        // Act
        await File.WriteAllTextAsync(Path.Combine(Root, "go"), "", Ct);
        await WaitForFileAsync("written", run);
        Time.Advance(ShellToolset.DrainTime);
        var result = await run;

        // Assert
        result.Output.Should().StartWith("late\n");
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

    // A command's marker file, or the end of a run that will never write it
    private async Task WaitForFileAsync(string name, Task run)
    {
        while (!File.Exists(Path.Combine(Root, name)) && !run.IsCompleted)
        {
            await Task.Yield();
        }
    }

    // The fake clock moves past the timeout until the command returns, however long it takes to start
    private async Task<CommandResult> RunPastTheTimeoutAsync(string command, ShellSettings settings)
    {
        var run = ShellToolset.RunCommandAsync(command, settings, Scope, Ct);

        while (!run.IsCompleted)
        {
            Time.Advance(settings.Timeout);
            await Task.Yield();
        }

        return await run;
    }
}
