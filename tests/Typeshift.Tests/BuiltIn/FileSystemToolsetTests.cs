using Typeshift.Tests.BuiltIn.Support;
using Typeshift.Toolsets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.BuiltIn;

public class FileSystemToolsetTests : WorkspaceTestBase
{
    [Fact]
    public async Task Given_A_File_Reading_It_Numbers_The_Lines_From_The_Offset()
    {
        // Arrange
        Write("notes.txt", "one\ntwo\nthree");

        // Act
        var read = await FileSystemToolset.ReadFileAsync("notes.txt", Files, Scope, offset: 1, limit: 1, ct: Ct);

        // Assert
        read.Should().Be("     2\ttwo\n[More lines follow from line 2.]");
    }

    [Fact]
    public async Task Given_A_Root_Path_The_File_Tools_Work_Within_It()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithFileSystemRoot(Root);

        Write("notes.txt", "one");
        await using var provider = services.BuildServiceProvider();

        // Act
        var read = await FileSystemToolset.ReadFileAsync("notes.txt", Files, provider, ct: Ct);

        // Assert
        read.Should().Be("     1\tone\n");
    }

    [Theory]
    [InlineData("/etc/passwd", "absolute")]
    [InlineData("../secret.txt", "'..'")]
    [InlineData("docs/../../secret.txt", "'..'")]
    public async Task Given_A_Path_Outside_The_Root_Reading_It_Is_Refused(string path, string reason)
    {
        // Act
        var read = await FileSystemToolset.ReadFileAsync(path, Files, Scope, ct: Ct);

        // Assert
        read.Should().Contain(reason);
    }

    [Fact]
    public async Task Given_A_Link_That_Leads_Outside_The_Root_Reading_Through_It_Is_Refused()
    {
        // Arrange
        var outside = Directory.CreateTempSubdirectory("typeshift-outside-").FullName;
        File.WriteAllText(Path.Combine(outside, "secret.txt"), Data.Agent.Reason);
        Directory.CreateSymbolicLink(Path.Combine(Root, "shared"), outside);

        // Act
        var read = await FileSystemToolset.ReadFileAsync("shared/secret.txt", Files, Scope, ct: Ct);

        // Assert
        read.Should().Contain("outside the workspace");
        Directory.Delete(outside, recursive: true);
    }

    [Fact]
    public async Task Given_A_Link_Whose_Target_Goes_Through_Another_Link_Outside_Using_It_Is_Refused()
    {
        // Arrange
        var outside = Directory.CreateTempSubdirectory("typeshift-outside-").FullName;
        var secret = Path.Combine(outside, "secret.txt");
        File.WriteAllText(secret, Data.Agent.Reason);
        Directory.CreateSymbolicLink(Path.Combine(Root, "shared"), outside);
        File.CreateSymbolicLink(Path.Combine(Root, "notes.txt"), "shared/secret.txt");

        // Act
        var read = await FileSystemToolset.ReadFileAsync("notes.txt", Files, Scope, ct: Ct);
        var deleted = FileSystemToolset.DeleteFile("notes.txt", Scope);

        // Assert
        read.Should().Contain("outside the workspace");
        deleted.Should().Contain("outside the workspace");
        File.Exists(secret).Should().BeTrue();
        Directory.Delete(outside, recursive: true);
    }

    [Fact]
    public async Task Given_A_Line_Over_The_Limit_Reading_And_Searching_Cut_It()
    {
        // Arrange
        Write("app.min.js", new string('x', Files.MaxLineLength * 10));

        // Act
        var read = await FileSystemToolset.ReadFileAsync("app.min.js", Files, Scope, ct: Ct);
        var found = await FileSystemToolset.SearchFilesAsync("x", Files, Scope, ct: Ct);

        // Assert
        read.Length.Should().BeLessThan(Files.MaxLineLength + 20);
        found.Should().EndWith("[cut]");
    }

    [Fact]
    public async Task Given_A_Pattern_That_Backtracks_Searching_Still_Ends_At_Once()
    {
        // Arrange
        Write("a.txt", string.Join('\n', Enumerable.Repeat(new string('a', 44) + "!", 20)));

        // Act
        var found = await FileSystemToolset.SearchFilesAsync("^(a+)+$", Files, Scope, ct: Ct);

        // Assert
        found.Should().Be("No line matches.");
    }

    [Fact]
    public async Task Given_A_New_Path_Writing_Creates_Its_Folders()
    {
        // Act
        await FileSystemToolset.WriteFileAsync("src/app/notes.txt", Data.Agent.Reason, Scope, Ct);

        // Assert
        File.ReadAllText(Path.Combine(Root, "src", "app", "notes.txt")).Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Text_That_Occurs_Once_Editing_Replaces_It()
    {
        // Arrange
        var file = Write("notes.txt", "keep this, change that");

        // Act
        await FileSystemToolset.EditFileAsync("notes.txt", "that", "this too", Scope, Ct);

        // Assert
        File.ReadAllText(file).Should().Be("keep this, change this too");
    }

    [Fact]
    public async Task Given_A_Text_That_Occurs_Twice_Editing_Is_Refused_And_The_File_Stays()
    {
        // Arrange
        var file = Write("notes.txt", "this and this");

        // Act
        var edited = await FileSystemToolset.EditFileAsync("notes.txt", "this", "that", Scope, Ct);

        // Assert
        edited.Should().Contain("more than once");
        File.ReadAllText(file).Should().Be("this and this");
    }

    [Fact]
    public async Task Given_Matching_Lines_Searching_Lists_Them_With_Their_Paths_And_Numbers()
    {
        // Arrange
        Write("a.txt", "alpha\nbeta");
        Write("docs/b.txt", "gamma\nalphabet");

        // Act
        var found = await FileSystemToolset.SearchFilesAsync("^alpha", Files, Scope, ct: Ct);

        // Assert
        found.Split('\n').Should().Equal("a.txt:1: alpha", "docs/b.txt:2: alphabet");
    }

    [Fact]
    public void Given_A_Folder_Listing_It_Shows_Its_Entries_With_Folders_Marked()
    {
        // Arrange
        Write("b.txt", "");
        Write("a/c.txt", "");

        // Act
        var listed = FileSystemToolset.ListFiles(Files, Scope);

        // Assert
        listed.Split('\n').Should().Equal("a/", "b.txt");
    }

    [Fact]
    public void Given_A_File_Deleting_Removes_It()
    {
        // Arrange
        var file = Write("notes.txt", "");

        // Act
        FileSystemToolset.DeleteFile("notes.txt", Scope);

        // Assert
        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public async Task Given_The_File_And_Shell_Tools_Those_That_Change_Things_Ask_And_The_Readers_Only_Read()
    {
        // Act
        var options = await Host<WorkspaceAgent>().ResolveOptionsAsync(ct: Ct);
        var tools = options.Tools!.ToList();

        // Assert
        tools.Where(tool => tool.GetService<Microsoft.Extensions.AI.ApprovalRequiredAIFunction>() is not null)
            .Select(tool => tool.Name)
            .Should()
            .Equal("delete_file", "run_command");
        tools.Where(tool => tool.GetService<Typeshift.Generated.GeneratedToolFunction>() is { IsReadOnly: true })
            .Select(tool => tool.Name)
            .Should()
            .Equal("read_file", "list_files", "search_files");
    }

    [Fact]
    public async Task Given_An_Agent_Using_The_Built_In_Toolset_Its_Configuration_Reaches_The_Tool()
    {
        // Arrange
        Write("notes.txt", "one\ntwo\nthree");

        ChatClient
            .Reply(Call(Data.Tool.CallId, "read_file", ("path", "notes.txt")))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ReaderAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ResultOf(ChatClient.Requests[1]).Should().Be("     1\tone\n     2\ttwo\n[More lines follow from line 2.]");
    }

    private static string ResultOf(IEnumerable<Microsoft.Extensions.AI.ChatMessage> request) =>
        request.SelectMany(message => message.Contents)
            .OfType<Microsoft.Extensions.AI.FunctionResultContent>()
            .Single()
            .Result is System.Text.Json.JsonElement json
            ? json.GetString()!
            : "";
}
