namespace Typeshift.Samples.Workspace;

// Stands in for the file and shell tools an app writes for its own workspace
internal partial class WorkspaceToolset : Toolset
{
    public WorkspaceToolset()
    {
        AddTool(ReadFile)
            .ReadOnly();

        AddTool(ListFiles)
            .ReadOnly();

        AddTool(SearchFiles)
            .ReadOnly();

        AddTool(RunCommand)
            .RequiresApproval();
    }

    /// <summary>
    /// Reads a file of the workspace.
    /// </summary>
    /// <param name="path">The file's path in the workspace.</param>
    public static string ReadFile(string path) => $"The contents of {path}.";

    /// <summary>
    /// Lists the files of a folder of the workspace.
    /// </summary>
    /// <param name="folder">The folder's path in the workspace.</param>
    public static string[] ListFiles(string folder) => [$"{folder}/README.md"];

    /// <summary>
    /// Finds the lines of the workspace's files that contain a text.
    /// </summary>
    /// <param name="text">The text to find.</param>
    public static string[] SearchFiles(string text) => [$"README.md:1: {text}"];

    /// <summary>
    /// Runs a shell command in the workspace.
    /// </summary>
    /// <param name="command">The command line.</param>
    public static string RunCommand(string command) => $"Ran {command}.";
}
