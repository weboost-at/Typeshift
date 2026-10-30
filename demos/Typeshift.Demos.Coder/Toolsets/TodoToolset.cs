using System.Text.Json.Serialization;

namespace Typeshift.Demos.Coder.Toolsets;

// The to-do list lives in the conversation: the latest call holds it, and compaction keeps the latest write_todos
internal partial class TodoToolset : Toolset
{
    public TodoToolset()
    {
        // It changes nothing outside the run, so plan mode lets it through
        AddTool(WriteTodos)
            .ReadOnly();
    }

    /// <summary>
    /// Replaces your to-do list. Plan multi-step work with it, keep exactly one item in progress, and mark items
    /// completed as soon as they are done.
    /// </summary>
    /// <param name="todos">The whole list, in order.</param>
    /// <returns>How the list stands.</returns>
    public static string WriteTodos(IReadOnlyList<TodoItem> todos) =>
        $"The list has {todos.Count} items: {todos.Count(todo => todo.Status == TodoStatus.Completed)} completed, "
        + $"{todos.Count(todo => todo.Status == TodoStatus.InProgress)} in progress.";
}

/// <summary>
/// One item of a to-do list.
/// </summary>
/// <param name="Title">What to do.</param>
/// <param name="Status">Where it stands.</param>
internal sealed record TodoItem(string Title, TodoStatus Status);

[JsonConverter(typeof(JsonStringEnumConverter<TodoStatus>))]
internal enum TodoStatus
{
    Pending,
    InProgress,
    Completed
}
