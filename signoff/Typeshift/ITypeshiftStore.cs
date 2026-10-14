using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// The one store Typeshift persists through. Each project sees it as <c>ITypeshiftStore</c>, an alias the generator
/// writes for the build's id type (<c>TypeshiftIdType</c>, <see cref="Guid"/> by default).
/// </summary>
/// <typeparam name="TId">
/// The id type of settings and conversations; the library never creates ids, it passes on the host's.
/// </typeparam>
public interface ITypeshiftStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads the document of kind <c>TData.Kind</c> stored under an id.
    /// </summary>
    /// <returns>The document and its revision, or null when none is stored.</returns>
    Task<Stored<TData>?> LoadAsync<TData>(TId id, CancellationToken ct) where TData : class, IStorable<TData>;

    /// <summary>
    /// Saves a document if the stored revision is still <paramref name="revision"/>, 0 for none.
    /// </summary>
    /// <returns>The new revision.</returns>
    /// <exception cref="StoreConflictException">Another save came first.</exception>
    Task<long> SaveAsync<TData>(TId id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>;

    /// <summary>
    /// Loads the entries of a conversation, oldest first.
    /// </summary>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Appends an entry to a conversation, creating it on the first.
    /// </summary>
    Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct);
}

/// <summary>
/// A stored document and the revision a save must name.
/// </summary>
public sealed record Stored<TData>(TData Data, long Revision);

/// <summary>
/// A document Typeshift stores: the agents' <c>Settings</c> and <see cref="ConversationEntry"/>, a closed set.
/// </summary>
/// <typeparam name="TSelf">The document type.</typeparam>
public interface IStorable<TSelf> where TSelf : class, IStorable<TSelf>
{
    /// <summary>
    /// The stable name a store keys the kind by: the agent's class name for its settings, so moving it to another
    /// namespace keeps its data.
    /// </summary>
    static abstract string Kind { get; }

    /// <summary>
    /// The schema version the store keeps next to the JSON and hands back to <see cref="Deserialize"/>.
    /// </summary>
    static abstract int Version { get; }

    /// <summary>
    /// Writes the document as JSON.
    /// </summary>
    string Serialize();

    /// <summary>
    /// Reads a document <see cref="Serialize"/> wrote under a version.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The version is newer than this code's.</exception>
    static abstract TSelf Deserialize(string json, int version);
}

/// <summary>
/// A save that lost to another save of the same document.
/// </summary>
public sealed class StoreConflictException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// One step of a conversation, appended as it happens: the input of a run, a model response, a tool result, or a
/// summary. A conversation is its entries in order; a run whose last entry does not end it was interrupted.
/// </summary>
public sealed record ConversationEntry(string Agent, IReadOnlyList<ChatMessage> Messages, DateTimeOffset At)
    : IStorable<ConversationEntry>
{
    /// <summary>
    /// The model that wrote the messages, for a model response.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// The tokens of the model call, for a model response.
    /// </summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>
    /// Whether the run ended with this entry: answered, failed, cancelled or waiting for approval.
    /// </summary>
    public bool EndsRun { get; init; }

    /// <summary>
    /// Whether this entry is a compaction summary that stands for every entry before it.
    /// </summary>
    public bool IsSummary { get; init; }

    public static string Kind => "conversation_entry";

    public static int Version => 1;

    public string Serialize() => throw null!;

    public static ConversationEntry Deserialize(string json, int version) => throw null!;
}

/// <summary>
/// A store in memory, for tests and samples. Each project sees it as <c>InMemoryTypeshiftStore</c>.
/// </summary>
public sealed class InMemoryTypeshiftStore<TId> : ITypeshiftStore<TId> where TId : notnull
{
    public Task<Stored<TData>?> LoadAsync<TData>(TId id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        throw null!;

    public Task<long> SaveAsync<TData>(TId id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        throw null!;

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct) =>
        throw null!;

    public Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct) =>
        throw null!;
}

/// <summary>
/// The conversations running in this process. Each project sees it as <c>ConversationTracker</c>.
/// </summary>
public sealed class ConversationTracker<TId> where TId : notnull
{
    internal ConversationTracker()
    {
    }

    /// <summary>
    /// The runs of conversations going on now.
    /// </summary>
    public IReadOnlyList<LiveRun<TId>> Runs => throw null!;

    /// <summary>
    /// The updates of a conversation's streaming runs as they happen, until cancelled; needs
    /// <c>WithRealtimeTracking()</c>.
    /// </summary>
    public IAsyncEnumerable<ChatResponseUpdate> WatchAsync(TId conversation, CancellationToken ct) => throw null!;

    /// <summary>
    /// Steers a run going on in this process: the input reaches the model before its next call, and the run does not
    /// end before it has. The input is stored with the conversation.
    /// </summary>
    /// <returns>False when no run of the conversation is going on here; start a run with the input instead.</returns>
    public bool TrySteer(TId conversation, string input) =>
        throw null!;
}

/// <summary>
/// A run going on now.
/// </summary>
public sealed record LiveRun<TId>(TId Conversation, string Agent, string Model, DateTimeOffset StartedAt);
