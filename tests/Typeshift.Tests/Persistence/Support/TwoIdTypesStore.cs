namespace Typeshift.Tests.Persistence.Support;

// Keeps settings under string ids and conversations under Guid ids, as an app with settings per tenant does. Asked
// to, it loads a conversation newest first, a mistake that only a check with ids of both types reaches
public sealed class TwoIdTypesStore(bool newestFirst = false)
    : ISettingsStore<string>, IConversationStore<Guid>, IAgentSettingsStore
{
    private readonly InMemoryTypeshiftStore<string> _settings = new();

    private readonly InMemoryTypeshiftStore<Guid> _conversations = new();

    public Task<StoredDocument?> LoadAsync(string id, string kind, CancellationToken cancellationToken) =>
        _settings.LoadAsync(id, kind, cancellationToken);

    public Task<long> SaveAsync(
        string id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _settings.SaveAsync(id, kind, json, loadedVersion, cancellationToken);

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken cancellationToken) =>
        _settings.LoadAsync(kind, cancellationToken);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken cancellationToken) =>
        _settings.SaveAsync(kind, json, loadedVersion, cancellationToken);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        var entries = await _conversations.LoadConversationAsync(conversation, cancellationToken);
        return newestFirst ? [.. entries.Reverse()] : entries;
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        (await LoadConversationAsync(conversation, cancellationToken)).SinceLastSummary();

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _conversations.AppendConversationAsync(conversation, entry, cancellationToken);
}
