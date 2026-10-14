using System.Text.Json.Serialization;

namespace Typeshift.Toolsets;

/// <summary>
/// A to-do list the model keeps while it works through a task: <c>write_todos</c>. The list lives in the conversation
/// itself, the latest call holds it, and compaction never drops that call.
/// </summary>
public sealed partial class TodoToolset : Toolset
{
    /// <summary>
    /// Declares <c>write_todos</c>, which only reads, as far as plan mode goes: it changes nothing outside the run.
    /// </summary>
    public TodoToolset()
    {
        WithDisplayName("Todos");

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
public sealed record TodoItem(string Title, TodoStatus Status);

/// <summary>
/// Where a to-do item stands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TodoStatus>))]
public enum TodoStatus
{
    /// <summary>
    /// Not started.
    /// </summary>
    Pending,

    /// <summary>
    /// Being worked on.
    /// </summary>
    InProgress,

    /// <summary>
    /// Done.
    /// </summary>
    Completed
}
