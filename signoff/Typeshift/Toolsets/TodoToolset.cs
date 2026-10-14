namespace Typeshift.Toolsets;

/// <summary>
/// A todo list the model keeps while it works through a task: <c>write_todos</c>. The list lives in the conversation
/// itself, the latest call holds it, and compaction never drops that call.
/// </summary>
public sealed partial class TodoToolset : Toolset
{
    public TodoToolset()
    {
        WithDisplayName("Todos");

        AddTool(WriteTodos)
            .ReadOnly();
    }

    /// <summary>
    /// Replaces your todo list. Plan multi-step work with it, keep exactly one item in progress, and mark items
    /// completed as soon as they are done.
    /// </summary>
    /// <param name="todos">The whole list, in order.</param>
    public static string WriteTodos(IReadOnlyList<TodoItem> todos) => throw null!;
}

/// <summary>
/// One item of a todo list.
/// </summary>
public sealed record TodoItem(string Title, TodoStatus Status);

/// <summary>
/// Where a todo item stands.
/// </summary>
public enum TodoStatus
{
    Pending,
    InProgress,
    Completed
}
