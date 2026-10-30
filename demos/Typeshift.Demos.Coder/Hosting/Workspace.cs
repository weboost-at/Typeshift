namespace Typeshift.Demos.Coder.Hosting;

// The folder the file and shell tools stay within, one per process here, one per customer in a web host
public sealed record WorkspaceOptions(string Path)
{
    private string Root => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(Path));

    // A path the model sent, within the workspace: relative, without .., and no further out once made full. A demo's
    // check: it follows no links, and run_command reaches past it anyway, which is why ShellGuard and approval stand
    // in front of the shell
    public string? Resolve(string path, out string? refused)
    {
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, path));

        refused = System.IO.Path.IsPathRooted(path) ? $"'{path}' is absolute; give a path relative to the workspace."
            : path.Split('/', '\\').Contains("..") ? $"'{path}' leads up with '..'; give a path within the workspace."
            : full != Root && !full.StartsWith(Root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? $"'{path}' leads outside the workspace."
                : null;

        return refused is null ? full : null;
    }

    public string Relative(string path) => System.IO.Path.GetRelativePath(Root, path).Replace('\\', '/');
}

// What the model knows about the workspace before it looks: its path and its top-level entries. Never stored, so
// every run sees the current state
public sealed class WorkspaceContext(WorkspaceOptions options) : IContextProvider
{
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct)
    {
        var entries = new DirectoryInfo(options.Path).EnumerateFileSystemInfos()
            .Select(entry => entry is DirectoryInfo ? $"{entry.Name}/" : entry.Name)
            .Order(StringComparer.Ordinal);

        return ValueTask.FromResult(new AgentContext
        {
            Instructions = $"The workspace is '{options.Path}'. Its top level holds: {string.Join(", ", entries)}."
        });
    }
}
