namespace Typeshift.Tests.Compaction.Support;

// A tool whose results are as long as the model asks, to fill a run's context
public partial class ReadingToolset : Toolset
{
    public ReadingToolset()
    {
        AddTool(Read);
    }

    /// <summary>
    /// Reads a page.
    /// </summary>
    /// <param name="length">How many characters it has.</param>
    public static string Read(int length) => new('x', length);
}

// A to-do tool of the app's own, whose latest call compaction keeps by its name
public partial class PlanningToolset : Toolset
{
    public PlanningToolset()
    {
        AddTool(WriteTodos);
    }

    /// <summary>
    /// Replaces the to-do list.
    /// </summary>
    /// <param name="todos">The whole list, in order.</param>
    public static string WriteTodos(IReadOnlyList<PlannedItem> todos) => $"The list has {todos.Count} items.";
}

public sealed record PlannedItem(string Title, string Status);

// Compacts once a test's run settings turn it on
public partial class CompactingAgent : Agent
{
    public const string ModelProvider = "compacting-provider";

    public const string Model = "compacting-model";

    public CompactingAgent()
    {
        WithChatOptions().WithModel(ModelProvider, Model);

        WithRunOptions()
            .Configurable(r => r.MaxInputTokens)
            .Configurable(r => r.TargetInputTokens)
            .Configurable(r => r.SummaryModel);

        UseToolset<ReadingToolset>();

        UseToolset<PlanningToolset>();
    }
}
