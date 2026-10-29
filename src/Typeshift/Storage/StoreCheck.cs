using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Checks a store of yours against the rules Typeshift relies on, as a test of the store. Each check writes under the
/// id it is given, so give it one nothing else uses, in a database for tests.
/// </summary>
// D58: what a store must do, run against the store itself, so a broken rule fails a test instead of a run
public static class StoreCheck
{
    private const string Kind = "typeshift_check";

    private const string OtherKind = "typeshift_check_other";

    private const string Agent = "typeshift_check";

    /// <summary>
    /// Saves and loads documents of two kinds under one id, and saves with a version another save replaced, as runs
    /// and settings forms do.
    /// </summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="id">A settings id nothing is stored under yet.</param>
    /// <param name="ct">Cancels the check.</param>
    /// <returns>A task that completes once the store passed.</returns>
    /// <exception cref="InvalidOperationException">
    /// The store broke a rule, or something is stored under the id already; the message says which.
    /// </exception>
    public static async Task CheckSettingsAsync<TId>(ISettingsStore<TId> store, TId id, CancellationToken ct = default)
        where TId : notnull
    {
        const string firstJson = """{"step":1}""";
        const string secondJson = """{"step":2}""";
        const string otherJson = """{"other":true}""";

        if (await store.LoadAsync(id, Kind, ct) is not null)
            throw new InvalidOperationException($"LoadAsync returned a document for {id}: give the check a new id.");

        var first = await SaveAsync(Kind, firstJson, 0, "A first save at version 0 threw a StoreConflictException.");
        await EnsureStoredAsync(Kind, new(firstJson, first), "LoadAsync did not return the last save and version.");

        var other = await SaveAsync(
            OtherKind,
            otherJson,
            0,
            "Saving a document of another kind under the same id conflicted: key documents by id and kind together.");

        await EnsureStoredAsync(
            Kind,
            new(firstJson, first),
            "Saving a document of another kind under the same id replaced this one: key documents by id and kind "
            + "together.");

        await EnsureStoredAsync(
            OtherKind,
            new(otherJson, other),
            "LoadAsync returned another kind's document: load by id and kind together.");

        await EnsureConflictAsync(
            0,
            new(firstJson, first),
            "A save at version 0 of a document stored already did not throw a StoreConflictException.");

        var second = await SaveAsync(
            Kind,
            secondJson,
            first,
            "A save with the version the last save returned threw a StoreConflictException.");

        if (second == first) throw Broken("SaveAsync returned the version the document had before the save.");

        await EnsureConflictAsync(
            first,
            new(secondJson, second),
            "A save with a version another save replaced did not throw a StoreConflictException.");

        async Task<long> SaveAsync(string kind, string json, long loadedVersion, string conflictRule)
        {
            try
            {
                var saved = await store.SaveAsync(id, kind, json, loadedVersion, ct);
                if (saved == 0) throw Broken("SaveAsync returned version 0, which tells Typeshift nothing is stored.");

                return saved;
            }
            catch (StoreConflictException exception)
            {
                throw Broken(conflictRule, exception);
            }
        }

        async Task EnsureStoredAsync(string kind, StoredDocument expected, string rule)
        {
            if (await store.LoadAsync(id, kind, ct) != expected) throw Broken(rule);
        }

        async Task EnsureConflictAsync(long loadedVersion, StoredDocument stored, string rule)
        {
            try
            {
                await store.SaveAsync(id, Kind, """{"step":0}""", loadedVersion, ct);
            }
            catch (StoreConflictException)
            {
                await EnsureStoredAsync(
                    Kind,
                    stored,
                    "A save that conflicted changed the document: check the version and write in one statement.");

                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw Broken($"{rule} It threw a {exception.GetType().Name}.", exception);
            }

            throw Broken(rule);
        }
    }

    /// <summary>
    /// Appends runs and compaction summaries to a conversation and loads them back, whole and from the last summary
    /// on, as runs do.
    /// </summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="conversation">A conversation id nothing is stored under yet.</param>
    /// <param name="ct">Cancels the check.</param>
    /// <returns>A task that completes once the store passed.</returns>
    /// <exception cref="InvalidOperationException">
    /// The store broke a rule, or something is stored under the id already; the message says which.
    /// </exception>
    public static async Task CheckConversationsAsync<TId>(
        IConversationStore<TId> store,
        TId conversation,
        CancellationToken ct = default)
        where TId : notnull
    {
        if ((await store.LoadConversationAsync(conversation, ct)).Count > 0)
        {
            throw new InvalidOperationException(
                $"LoadConversationAsync returned entries for {conversation}: give the check a new id.");
        }

        ConversationEntry[] entries =
        [
            Entry(1, ChatRole.User),
            RunEnd(2),
            Summary(3),
            RunEnd(4),
            Summary(5),
            RunEnd(6)
        ];

        const string order = "LoadConversationAsync did not return the entries in the order appended, oldest first.";

        await AppendAsync(entries[..2]);
        EnsureSame(await store.LoadConversationAsync(conversation, ct), entries[..2], order);

        EnsureSame(
            await store.LoadConversationSinceCompactionAsync(conversation, ct),
            entries[..2],
            "LoadConversationSinceCompactionAsync did not return every entry of a conversation with no summary.");

        await AppendAsync(entries[2..]);
        EnsureSame(await store.LoadConversationAsync(conversation, ct), entries, order);

        EnsureSame(
            await store.LoadConversationSinceCompactionAsync(conversation, ct),
            entries[4..],
            "LoadConversationSinceCompactionAsync did not start at the last summary.");

        async Task AppendAsync(ConversationEntry[] appended)
        {
            foreach (var entry in appended)
            {
                await store.AppendConversationAsync(conversation, entry, ct);
            }
        }
    }

    // A fixed time each, so the check reads no clock and the entries tell themselves apart
    private static ConversationEntry Entry(int step, ChatRole role) =>
        new(Agent, [new ChatMessage(role, $"Step {step}.")], DateTimeOffset.UnixEpoch.AddSeconds(step));

    private static ConversationEntry RunEnd(int step) =>
        Entry(step, ChatRole.Assistant) with
        {
            EndsRun = true
        };

    private static ConversationEntry Summary(int step) =>
        Entry(step, ChatRole.User) with
        {
            IsSummary = true,
            StandingApprovals = []
        };

    private static void EnsureSame(IReadOnlyList<ConversationEntry> loaded, ConversationEntry[] appended, string rule)
    {
        if (!loaded.Select(entry => entry.At).SequenceEqual(appended.Select(entry => entry.At))) throw Broken(rule);

        var changed = !loaded
            .Select(entry => entry.Serialize())
            .SequenceEqual(appended.Select(entry => entry.Serialize()));

        if (changed) throw Broken("An entry came back changed: store the JSON of entry.Serialize() as it is.");
    }

    private static InvalidOperationException Broken(string rule, Exception? inner = null) =>
        new($"The store broke a rule Typeshift relies on. {rule}", inner);
}
