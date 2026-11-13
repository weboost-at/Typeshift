using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Verifies a store of yours against the rules Typeshift relies on, from a test of the store.
/// </summary>
// D58: what a store must do, run against the store itself, so a broken rule fails a test instead of a run
public static class StoreExtensions
{
    private const string Kind = "typeshift_check";

    private const string Agent = "typeshift_check";

    private const string ById = "key documents by id and kind together.";

    extension<TId>(ISettingsStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for settings against this store, and the rules for conversations and agents' own settings
        /// too when it keeps those as well. It writes under both ids, so give it two that nothing else uses, in a
        /// database for tests.
        /// </summary>
        /// <param name="id">An id nothing is stored under yet, as a settings id and as a conversation id.</param>
        /// <param name="otherId">
        /// A second such id. What the check stores under it must stay as it is while the check writes under the first,
        /// which tells a store that forgets the id.
        /// </param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under one of the ids already; the message says which.
        /// </exception>
        /// <exception cref="ArgumentException">The two ids are equal.</exception>
        public Task VerifyAsync(TId id, TId otherId, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store, store as IConversationStore<TId>, id, otherId, ct);
    }

    extension<TId>(IConversationStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for conversations against this store, and the rules for settings and agents' own settings
        /// too when it keeps those as well. It writes under both ids, so give it two that nothing else uses, in a
        /// database for tests.
        /// </summary>
        /// <param name="id">An id nothing is stored under yet, as a conversation id and as a settings id.</param>
        /// <param name="otherId">
        /// A second such id. What the check stores under it must stay as it is while the check writes under the first,
        /// which tells a store that forgets the id.
        /// </param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under one of the ids already; the message says which.
        /// </exception>
        /// <exception cref="ArgumentException">The two ids are equal.</exception>
        public Task VerifyAsync(TId id, TId otherId, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store as ISettingsStore<TId>, store, id, otherId, ct);
    }

    // A store that is both would match both blocks above; this one is more specific, so the call compiles
    extension<TId>(ITypeshiftStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for settings and for conversations against this store, and for agents' own settings when it
        /// keeps those too. It writes under both ids, so give it two that nothing else uses, in a database for tests.
        /// </summary>
        /// <param name="id">An id nothing is stored under yet, as a settings id and as a conversation id.</param>
        /// <param name="otherId">
        /// A second such id. What the check stores under it must stay as it is while the check writes under the first,
        /// which tells a store that forgets the id.
        /// </param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, or something is stored under one of the ids already; the message says which.
        /// </exception>
        /// <exception cref="ArgumentException">The two ids are equal.</exception>
        public Task VerifyAsync(TId id, TId otherId, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store, store, id, otherId, ct);
    }

    extension(IAgentSettingsStore store)
    {
        /// <summary>
        /// Runs the rules for agents' own settings against this store. It writes documents of the kind it is given and
        /// of that kind with <c>_other</c> after it, and tries one with <c>_missing</c> after it that a store must
        /// refuse, so give it a kind nothing else uses, in a database for tests. <c>VerifyAsync</c> runs these rules
        /// too, for a store that also keeps settings ids or conversations.
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

    // D66: a store that keeps agents' own settings too is checked under kinds named after the id, since they take no
    // id, and its agents' own documents must stay apart from an id's of the same kind
    private static async Task VerifyEveryInterfaceAsync<TId>(
        object store,
        ISettingsStore<TId>? settings,
        IConversationStore<TId>? conversations,
        TId id,
        TId otherId,
        CancellationToken ct)
        where TId : notnull
    {
        if (EqualityComparer<TId>.Default.Equals(id, otherId))
        {
            throw new ArgumentException(
                "VerifyAsync needs two different ids: what it stores under the second shows whether the store tells "
                + "ids apart.",
                nameof(otherId));
        }

        if (settings is not null) await VerifySettingsAsync(settings, id, otherId, ct);
        if (conversations is not null) await VerifyConversationsAsync(conversations, id, otherId, ct);
        if (store is not IAgentSettingsStore agents) return;

        await VerifyAgentDocumentsAsync(agents, $"{Kind}_{id}", ct);
        if (settings is not null) await VerifyApartAsync(settings, agents, id, $"{Kind}_{id}_apart", ct);
    }

    // A store that keeps both in one table and forgets the id in a load of an agent's own document would hand every
    // id's settings to all of them
    private static async Task VerifyApartAsync<TId>(
        ISettingsStore<TId> settings,
        IAgentSettingsStore agents,
        TId id,
        string kind,
        CancellationToken ct)
        where TId : notnull
    {
        const string idJson = """{"of":"id"}""";
        const string agentJson = """{"of":"agent"}""";
        const string apart = "keep an agent's own documents apart from the documents of the same kind under an id.";

        var saved = await settings.SaveAsync(id, kind, idJson, 0, ct);

        if (await agents.LoadAsync(kind, ct) is not null)
            throw Broken($"An agent's own load returned a document saved under an id: {apart}");

        long own;

        try
        {
            own = await agents.SaveAsync(kind, agentJson, 0, ct);
        }
        catch (StoreConflictException exception)
        {
            throw Broken(
                $"A first save of an agent's own document conflicted with one under an id: {apart}",
                exception);
        }

        if (await settings.LoadAsync(id, kind, ct) != new StoredDocument(idJson, saved))
            throw Broken($"Saving an agent's own document replaced the one under an id: {apart}");

        if (await agents.LoadAsync(kind, ct) != new StoredDocument(agentJson, own))
            throw Broken($"An agent's own load did not return its last save and version: {apart}");
    }

    // A document under another id first, which the saves under the id must leave alone: a store that forgets the id
    // in a load or a save would hand one customer's settings to another
    private static async Task VerifySettingsAsync<TId>(
        ISettingsStore<TId> store,
        TId id,
        TId otherId,
        CancellationToken ct)
        where TId : notnull
    {
        const string otherJson = """{"of":"another id"}""";

        foreach (var taken in (TId[])[id, otherId])
        {
            if (await store.LoadAsync(taken, Kind, ct) is not null) throw new InvalidOperationException(Taken(taken));
        }

        long other;

        try
        {
            other = await store.SaveAsync(otherId, Kind, otherJson, 0, ct);
        }
        catch (StoreConflictException exception)
        {
            throw Broken("A first save at version 0 threw a StoreConflictException.", exception);
        }

        if (await store.LoadAsync(id, Kind, ct) is not null)
            throw Broken($"LoadAsync returned a document saved under another id: {ById}");

        await VerifyDocumentsAsync(
            kind => store.LoadAsync(id, kind, ct),
            (kind, json, loadedVersion) => store.SaveAsync(id, kind, json, loadedVersion, ct),
            new(Kind, Taken(id), " under the same id", "id and kind together"));

        if (await store.LoadAsync(otherId, Kind, ct) != new StoredDocument(otherJson, other))
            throw Broken($"Saving a document under one id changed the one under another id: {ById}");
    }

    private static string Taken<TId>(TId id) => $"LoadAsync returned a document for {id}: give VerifyAsync a new id.";

    // D66: an agent's own settings, keyed by the kind alone
    private static Task VerifyAgentDocumentsAsync(IAgentSettingsStore store, string kind, CancellationToken ct) =>
        VerifyDocumentsAsync(
            document => store.LoadAsync(document, ct),
            (document, json, loadedVersion) => store.SaveAsync(document, json, loadedVersion, ct),
            new(
                kind,
                $"LoadAsync returned a document of the kind {kind}: give the check a kind nothing is stored under.",
                "",
                "kind"));

    // Saves and loads documents of two kinds, and saves with a version another save replaced or of a document that is
    // not stored, as runs and settings forms do
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
            $"A first save at version 0 threw a StoreConflictException: key documents by {keys.Key}.");

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
            keys.Kind,
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
            keys.Kind,
            first,
            new(secondJson, second),
            "A save with a version another save replaced did not throw a StoreConflictException.");

        // A document deleted since the caller loaded it: an update that inserts the missing row brings it back
        await EnsureConflictAsync(
            keys.MissingKind,
            second,
            null,
            "A save at a version other than 0 of a document that is not stored did not throw a "
            + "StoreConflictException: update only a row that holds the version.");

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

        async Task EnsureStoredAsync(string kind, StoredDocument? expected, string rule)
        {
            if (await load(kind) != expected) throw Broken(rule);
        }

        async Task EnsureConflictAsync(string kind, long loadedVersion, StoredDocument? stored, string rule)
        {
            try
            {
                await save(kind, """{"step":0}""", loadedVersion);
            }
            catch (StoreConflictException)
            {
                await EnsureStoredAsync(
                    kind,
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
    // on, as runs do, beside another conversation that must load as it was
    private static async Task VerifyConversationsAsync<TId>(
        IConversationStore<TId> store,
        TId conversation,
        TId other,
        CancellationToken ct)
        where TId : notnull
    {
        foreach (var taken in (TId[])[conversation, other])
        {
            if ((await store.LoadConversationAsync(taken, ct)).Count > 0)
            {
                throw new InvalidOperationException(
                    $"LoadConversationAsync returned entries for {taken}: give VerifyAsync a new id.");
            }
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

        ConversationEntry[] elsewhere = [Entry(0, ChatRole.User)];
        HashSet<string> appended = [.. entries.Concat(elsewhere).Select(entry => entry.Serialize())];

        const string order =
            "LoadConversationAsync did not return the entries in the order appended, oldest first: order them by a "
            + "number the store assigns as it appends, never by their times, which tie and run backwards.";

        await store.AppendConversationAsync(other, elsewhere[0], ct);

        if ((await store.LoadConversationAsync(conversation, ct)).Count > 0)
        {
            throw Broken(
                "LoadConversationAsync returned entries appended to another conversation: key entries by the "
                + "conversation id.");
        }

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

        EnsureSame(
            await store.LoadConversationSinceCompactionAsync(other, ct),
            elsewhere,
            "LoadConversationSinceCompactionAsync started a conversation at another conversation's summary: look for "
            + "the last summary of the conversation it is given.");

        async Task AppendAsync(ConversationEntry[] steps)
        {
            foreach (var entry in steps)
            {
                await store.AppendConversationAsync(conversation, entry, ct);
            }
        }

        // Entries that all came back as they were appended are the wrong ones or in the wrong order
        void EnsureSame(IReadOnlyList<ConversationEntry> loaded, ConversationEntry[] expected, string rule)
        {
            var json = loaded.Select(entry => entry.Serialize()).ToList();
            if (json.SequenceEqual(expected.Select(entry => entry.Serialize()))) return;

            throw json.All(appended.Contains)
                ? Broken(rule)
                : Broken("An entry came back changed: store the JSON of entry.Serialize() as it is.");
        }
    }

    // Fixed times, so the check reads no clock, that neither rise nor fall with the order appended and tie, as the
    // times of real entries can: a store that orders by them fails
    private static ConversationEntry Entry(int step, ChatRole role) =>
        new(Agent, [new ChatMessage(role, $"Step {step}.")], DateTimeOffset.UnixEpoch.AddSeconds(step * 2 % 3));

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

    private static InvalidOperationException Broken(string rule, Exception? inner = null) =>
        new($"The store broke a rule Typeshift relies on. {rule}", inner);

    // The kinds a check writes, what it says when the first is taken, and how documents are keyed
    private sealed record DocumentKeys(string Kind, string Taken, string Scope, string Key)
    {
        public string OtherKind => $"{Kind}_other";

        // Never stored by a store that keeps the rules
        public string MissingKind => $"{Kind}_missing";
    }
}
