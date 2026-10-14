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

// Compacts once a test's run settings turn it on
public partial class CompactingAgent : Agent
{
    public CompactingAgent()
    {
        WithChatOptions().WithModel("compacting-model");

        WithRunOptions()
            .Configurable(r => r.MaxInputTokens)
            .Configurable(r => r.TargetInputTokens)
            .Configurable(r => r.SummaryModel);

        UseToolset<ReadingToolset>();
    }
}
