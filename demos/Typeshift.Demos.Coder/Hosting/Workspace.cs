using Typeshift.Toolsets;

namespace Typeshift.Demos.Coder.Hosting;

// The folder the file and shell tools stay within, one per process here, one per customer in a web host
public sealed record WorkspaceOptions(string Path);

public sealed class WorkspaceRoot(WorkspaceOptions options) : IFileSystemRoot
{
    public string Path => options.Path;
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
