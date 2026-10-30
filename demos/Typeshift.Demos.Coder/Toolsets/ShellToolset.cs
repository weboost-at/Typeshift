using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Coder.Hosting;

namespace Typeshift.Demos.Coder.Toolsets;

// One command at a time in the workspace; ShellGuard decides the obvious calls before the host is asked
internal partial class ShellToolset : Toolset
{
    public ShellToolset()
    {
        AddTool(RunCommandAsync)
            .RequiresApproval()
            .WithSettings<RunCommandSettings>();
    }

    /// <summary>
    /// Runs a shell command in the workspace and returns what it wrote, such as a test run's results.
    /// </summary>
    /// <param name="command">The command line.</param>
    /// <param name="settings">The shell and the timeout.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the command.</param>
    /// <returns>The exit code and both streams; exit code -1 when the command timed out.</returns>
    public static async Task<CommandResult> RunCommandAsync(
        string command,
        RunCommandSettings settings,
        IServiceProvider services,
        CancellationToken ct)
    {
        var start = new ProcessStartInfo(settings.Shell)
        {
            WorkingDirectory = services.GetRequiredService<WorkspaceOptions>().Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(command);

        using var limit = new CancellationTokenSource(settings.Timeout, services.GetRequiredService<TimeProvider>());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, limit.Token);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"The shell '{settings.Shell}' did not start.");

        process.StandardInput.Close();

        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult(-1, await output, await error, TimedOut: true);
        }

        return new CommandResult(process.ExitCode, await output, await error, TimedOut: false);
    }
}

/// <summary>
/// The shell run_command uses and how long a command may take.
/// </summary>
internal sealed record RunCommandSettings
{
    /// <summary>
    /// The shell, run as <c>{Shell} -c {command}</c>.
    /// </summary>
    public string Shell { get; init; } = "/bin/sh";

    /// <summary>
    /// How long a command may run before it is killed.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// What a command did.
/// </summary>
/// <param name="ExitCode">The exit code; -1 when the command timed out.</param>
/// <param name="Output">What it wrote to standard output.</param>
/// <param name="Error">What it wrote to standard error.</param>
/// <param name="TimedOut">Whether it ran out of time and was killed.</param>
internal sealed record CommandResult(int ExitCode, string Output, string Error, bool TimedOut);
