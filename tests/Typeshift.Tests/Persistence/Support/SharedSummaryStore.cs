namespace Typeshift.Tests.Persistence.Support;

// Starts a run's load at the last summary of any conversation, as a query for the last summary's row that forgets
// the conversation id would; whole conversations load right
public sealed class SharedSummaryStore : IConversationStore<Guid>
{
    private readonly List<(Guid Conversation, string Json)> _rows = [];

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        Task.FromResult(Load(conversation, start: 0));

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct)
    {
        lock (_rows)
        {
            var summary = _rows.FindLastIndex(row => ConversationEntry.Deserialize(row.Json).IsSummary);
            return Task.FromResult(Load(conversation, Math.Max(summary, 0)));
        }
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        lock (_rows) _rows.Add((conversation, entry.Serialize()));
        return Task.CompletedTask;
    }

    private IReadOnlyList<ConversationEntry> Load(Guid conversation, int start)
    {
        lock (_rows)
        {
            return
            [
                .. _rows.Skip(start)
                    .Where(row => row.Conversation == conversation)
                    .Select(row => ConversationEntry.Deserialize(row.Json))
            ];
        }
    }
}
