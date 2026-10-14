using Typeshift.Tests.Agents.Support;
using Typeshift.Toolsets;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.BuiltIn.Support;

// A folder of its own per test, as the root of the file system and shell tools, removed afterwards
public abstract class WorkspaceTestBase : AgentTestBase, IDisposable
{
    protected WorkspaceTestBase()
    {
        Root = Directory.CreateTempSubdirectory("typeshift-").FullName;
    }

    protected string Root { get; }

    protected FileSystemSettings Files { get; } = new();

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift().WithFileSystemRoot<Workspace>();
        services.AddSingleton(new WorkspacePath(Root));
    }

    protected string Write(string path, string content)
    {
        var file = Path.Combine(Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }
}

public sealed record WorkspacePath(string Value);

public sealed class Workspace(WorkspacePath path) : IFileSystemRoot
{
    public string Path => path.Value;
}

// Reads files through the built-in toolset of the library, a toolset of a referenced assembly
public partial class ReaderAgent : Agent
{
    public const int Lines = 2;

    public ReaderAgent()
    {
        WithChatOptions().WithModel("reader-model");

        UseToolset<FileSystemToolset>().Include(t => t.ReadFile);

        UseToolset<TodoToolset>();

        ConfigureReadFile()
            .WithMaxLines(Lines);
    }
}
