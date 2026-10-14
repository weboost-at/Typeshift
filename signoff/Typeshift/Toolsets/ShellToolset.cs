namespace Typeshift.Toolsets;

/// <summary>
/// Runs shell commands in the root the host's <see cref="IFileSystemRoot"/> gives, each behind the host's approval.
/// The root is the working folder, not a sandbox: run the host in a container when commands must not reach further.
/// </summary>
public sealed partial class ShellToolset : Toolset
{
    public ShellToolset()
    {
        WithDisplayName("Shell");

        AddTool(RunCommandAsync)
            .RequiresApproval()
            .WithSettings<ShellSettings>();
    }

    /// <summary>
    /// Runs a shell command and returns its exit code and output.
    /// </summary>
    /// <param name="command">The command line, for the shell the settings name.</param>
    public static Task<CommandResult> RunCommandAsync(
        string command,
        ShellSettings settings,
        IServiceProvider services,
        CancellationToken ct) =>
        throw null!;
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
public sealed record CommandResult(int ExitCode, string Output, string Error, bool TimedOut);
