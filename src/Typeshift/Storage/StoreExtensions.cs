using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Verifies a store of yours against the rules Typeshift relies on, from a test of the store.
/// </summary>
// D58: what a store must do, run against the store itself, so a broken rule fails a test instead of a run
public static class StoreExtensions
{
    private const string Kind = "typeshift_check";

    private const string OtherKind = "typeshift_check_other";

    private const string Agent = "typeshift_check";

    extension<TId>(ISettingsStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for settings against this store, and the rules for conversations and agents' own settings
        /// too when it keeps those as well. It writes under the id it is given, so give it one nothing else uses, in a
        /// database for tests.
        /// </summary>
        /// <param name="id">An id nothing is stored under yet, as a settings id and as a conversation id.</param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under the id already; the message says which.
        /// </exception>
        public Task VerifyAsync(TId id, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store, store as IConversationStore<TId>, id, ct);
    }

    extension<TId>(IConversationStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for conversations against this store, and the rules for settings and agents' own settings
        /// too when it keeps those as well. It writes under the id it is given, so give it one nothing else uses, in a
        /// database for tests.
        /// </summary>
        /// <param name="id">An id nothing is stored under yet, as a conversation id and as a settings id.</param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under the id already; the message says which.
        /// </exception>
        public Task VerifyAsync(TId id, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store as ISettingsStore<TId>, store, id, ct);
    }

    // A store that is both would match both blocks above; this one is more specific, so the call compiles
    extension<TId>(ITypeshiftStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for settings and for conversations against this store, and for agents' own settings when it
        /// keeps those too. It writes under the id it is given, so give it one nothing else uses, in a database for
        /// tests.
        /// </summary>
        /// <param name="id">An id nothing is stored under yet, as a settings id and as a conversation id.</param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under the id already; the message says which.
        /// </exception>
        public Task VerifyAsync(TId id, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store, store, id, ct);
    }

    extension(IAgentSettingsStore store)
    {
        /// <summary>
        /// Runs the rules for agents' own settings against this store. It writes documents of the kind it is given and
        /// of that kind with <c>_other</c> after it, so give it a kind nothing else uses, in a database for tests.
        /// <c>VerifyAsync</c> runs these rules too, for a store that also keeps settings ids or conversations.
        /// </summary>
        /// <param name="kind">A kind nothing is stored under yet.</param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under the kind already; the message says which.
        /// </exception>
        public Task VerifyAgentSettingsAsync(string kind, CancellationToken ct = default) =>
            VerifyAgentDocumentsAsync(store, kind, ct);
    }

    // D66: a store that keeps agents' own settings too is checked under kinds named after the id, as fresh as it is
    private static async Task VerifyEveryInterfaceAsync<TId>(
        object store,
        ISettingsStore<TId>? settings,
        IConversationStore<TId>? conversations,
        TId id,
        CancellationToken ct)
        where TId : notnull
    {
        if (settings is not null) await VerifySettingsAsync(settings, id, ct);
        if (conversations is not null) await VerifyConversationsAsync(conversations, id, ct);
        if (store is IAgentSettingsStore agents) await VerifyAgentDocumentsAsync(agents, $"{Kind}_{id}", ct);
    }

    private static Task VerifySettingsAsync<TId>(ISettingsStore<TId> store, TId id, CancellationToken ct)
        where TId : notnull =>
        VerifyDocumentsAsync(
            kind => store.LoadAsync(id, kind, ct),
            (kind, json, loadedVersion) => store.SaveAsync(id, kind, json, loadedVersion, ct),
            new(
                Kind,
                OtherKind,
                $"LoadAsync returned a document for {id}: give VerifyAsync a new id.",
                " under the same id",
                "id and kind together"));

    // D66: an agent's own settings, keyed by the kind alone
    private static Task VerifyAgentDocumentsAsync(IAgentSettingsStore store, string kind, CancellationToken ct) =>
        VerifyDocumentsAsync(
            document => store.LoadAsync(document, ct),
            (document, json, loadedVersion) => store.SaveAsync(document, json, loadedVersion, ct),
            new(
                kind,
                $"{kind}_other",
                $"LoadAsync returned a document of the kind {kind}: give the check a kind nothing is stored under.",
                "",
                "kind"));

    // Saves and loads documents of two kinds, and saves with a version another save replaced, as runs and settings
    // forms do
    private static async Task VerifyDocumentsAsync(
        Func<string, Task<StoredDocument?>> load,
        Func<string, string, long, Task<long>> save,
        DocumentKeys keys)
    {
        const string firstJson = """{"step":1}""";
        const string secondJson = """{"step":2}""";
        const string otherJson = """{"other":true}""";

        if (await load(keys.Kind) is not null) throw new InvalidOperationException(keys.Taken);

        var first = await SaveAsync(
            keys.Kind,
            firstJson,
            0,
            "A first save at version 0 threw a StoreConflictException.");

        await EnsureStoredAsync(
            keys.Kind,
            new(firstJson, first),
            "LoadAsync did not return the last save and version.");

        var other = await SaveAsync(
            keys.OtherKind,
            otherJson,
            0,
            $"Saving a document of another kind{keys.Scope} conflicted: key documents by {keys.Key}.");

        await EnsureStoredAsync(
            keys.Kind,
            new(firstJson, first),
            $"Saving a document of another kind{keys.Scope} replaced this one: key documents by {keys.Key}.");

        await EnsureStoredAsync(
            keys.OtherKind,
            new(otherJson, other),
            $"LoadAsync returned another kind's document: load by {keys.Key}.");

        await EnsureConflictAsync(
            0,
            new(firstJson, first),
            "A save at version 0 of a document stored already did not throw a StoreConflictException.");

        var second = await SaveAsync(
            keys.Kind,
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
                var saved = await save(kind, json, loadedVersion);
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
            if (await load(kind) != expected) throw Broken(rule);
        }

        async Task EnsureConflictAsync(long loadedVersion, StoredDocument stored, string rule)
        {
            try
            {
                await save(keys.Kind, """{"step":0}""", loadedVersion);
            }
            catch (StoreConflictException)
            {
                await EnsureStoredAsync(
                    keys.Kind,
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

    // Appends runs and compaction summaries to a conversation and loads them back, whole and from the last summary
    // on, as runs do
    private static async Task VerifyConversationsAsync<TId>(
        IConversationStore<TId> store,
        TId conversation,
        CancellationToken ct)
        where TId : notnull
    {
        if ((await store.LoadConversationAsync(conversation, ct)).Count > 0)
        {
            throw new InvalidOperationException(
                $"LoadConversationAsync returned entries for {conversation}: give VerifyAsync a new id.");
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

    // The kinds a check writes, what it says when the first is taken, and how documents are keyed
    private sealed record DocumentKeys(string Kind, string OtherKind, string Taken, string Scope, string Key);
}
