namespace Typeshift.Tests.Persistence.Support;

// Keeps settings under string ids and conversations under Guid ids, as an app with settings per tenant does. Asked
// to, it loads a conversation newest first, a mistake that only a check with ids of both types reaches
public sealed class TwoIdTypesStore(bool newestFirst = false)
    : ISettingsStore<string>, IConversationStore<Guid>, IAgentSettingsStore
{
    private readonly InMemoryTypeshiftStore<string> _settings = new();

    private readonly InMemoryTypeshiftStore<Guid> _conversations = new();

    public Task<StoredDocument?> LoadAsync(string id, string kind, CancellationToken ct) =>
        _settings.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(string id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _settings.SaveAsync(id, kind, json, loadedVersion, ct);

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) => _settings.LoadAsync(kind, ct);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
        _settings.SaveAsync(kind, json, loadedVersion, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var entries = await _conversations.LoadConversationAsync(conversation, ct);
        return newestFirst ? [.. entries.Reverse()] : entries;
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _conversations.AppendConversationAsync(conversation, entry, ct);
}
