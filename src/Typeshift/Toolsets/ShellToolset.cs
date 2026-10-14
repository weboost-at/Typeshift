using System.Diagnostics;
using System.Text;

namespace Typeshift.Toolsets;

/// <summary>
/// Runs shell commands in the root the host's <see cref="IFileSystemRoot"/> gives, each behind the host's approval.
/// The root is the working folder, not a sandbox: run the host in a container when commands must not reach further. A
/// process a command leaves running in the background outlives the call, which returns at its timeout.
/// </summary>
public sealed partial class ShellToolset : Toolset
{
    /// <summary>
    /// Declares <c>run_command</c>, which asks first.
    /// </summary>
    public ShellToolset()
    {
        WithDisplayName("Shell");

        AddTool(RunCommandAsync)
            .RequiresApproval()
            .WithSettings<ShellSettings>();
    }

    /// <summary>
    /// Runs a shell command in the workspace and returns its exit code and output.
    /// </summary>
    /// <param name="command">The command line, for the shell the settings name.</param>
    /// <param name="settings">The shell, the timeout and the output's limit.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the command, which is killed.</param>
    /// <returns>What the command did.</returns>
    public static async Task<CommandResult> RunCommandAsync(
        string command,
        ShellSettings settings,
        IServiceProvider services,
        CancellationToken ct)
    {
        var start = new ProcessStartInfo(settings.Shell)
        {
            WorkingDirectory = FileSystemRoot.Of(services),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(command);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"The shell '{settings.Shell}' did not start.");

        process.StandardInput.Close();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.Timeout);

        // A process the command left running in the background keeps the streams open; reading them ends with the
        // timeout as well
        var output = Capture(process.StandardOutput, settings.MaxOutputLength, timeout.Token);
        var error = Capture(process.StandardError, settings.MaxOutputLength, timeout.Token);

        var timedOut = false;

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            ct.ThrowIfCancellationRequested();
            timedOut = true;
        }

        var result = new CommandResult(timedOut ? -1 : process.ExitCode, await output, await error, timedOut);
        ct.ThrowIfCancellationRequested();

        return result;
    }

    // The stream's start and end, within the limit, read to its end so the process never blocks on a full pipe
    private static async Task<string> Capture(StreamReader stream, int limit, CancellationToken stop)
    {
        var start = new StringBuilder();
        var end = new Queue<char>();
        var skipped = 0L;
        var buffer = new char[4096];

        while (await ReadAsync(stream, buffer, stop) is var read and > 0)
        {
            foreach (var character in buffer.AsSpan(0, read))
            {
                if (start.Length < limit / 2)
                {
                    start.Append(character);
                    continue;
                }

                end.Enqueue(character);
                if (end.Count > limit / 2 && end.TryDequeue(out _)) skipped++;
            }
        }

        return skipped == 0
            ? start.Append(end.ToArray()).ToString()
            : $"{start}\n[{skipped} characters left out]\n{new string([.. end])}";
    }

    // Stopping reads as the end of the stream
    private static async Task<int> ReadAsync(StreamReader stream, char[] buffer, CancellationToken stop)
    {
        try
        {
            return await stream.ReadAsync(buffer, stop);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }
}

/// <summary>
/// How commands run.
/// </summary>
public sealed record ShellSettings
{
    /// <summary>
    /// The shell, run as <c>{Shell} -c {command}</c>.
    /// </summary>
    public string Shell { get; init; } = "/bin/sh";

    /// <summary>
    /// How long a command may run before it is killed.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The most characters of each stream the model receives, its start and its end.
    /// </summary>
    public int MaxOutputLength { get; init; } = 64 * 1024;
}

/// <summary>
/// What a command did.
/// </summary>
/// <param name="ExitCode">The exit code; -1 when the command timed out.</param>
/// <param name="Output">What it wrote to standard output.</param>
/// <param name="Error">What it wrote to standard error.</param>
/// <param name="TimedOut">Whether it ran out of time and was killed.</param>
public sealed record CommandResult(int ExitCode, string Output, string Error, bool TimedOut);
