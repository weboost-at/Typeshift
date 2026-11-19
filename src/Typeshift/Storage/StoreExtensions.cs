using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Verifies a store of yours against the rules Typeshift relies on, from a test of the store.
/// </summary>
// D58: what a store must do, run against the store itself, so a broken rule fails a test instead of a run. A store of
// several interfaces matches the block of each, which is ambiguous to the compiler, so the blocks have priorities:
// settings before conversations before decisions. Every block runs the rules of all the store implements
public static class StoreExtensions
{
    private const string Kind = "typeshift_check";

    private const string Agent = "typeshift_check";

    private const string ById = "key documents by id and kind together.";

    extension<TId>(ISettingsStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for settings against this store, and the rules for conversations, decisions and agents' own
        /// settings too when it keeps those as well. It writes under both ids, so give it two that nothing else uses,
        /// in a database for tests. Conversations under ids of another type are left out: check those with the
        /// overload that takes the ids of both types.
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
        [OverloadResolutionPriority(2)]
        public Task VerifyAsync(TId id, TId otherId, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store, (id, otherId), store as IConversationStore<TId>, (id, otherId), ct);

        /// <summary>
        /// Runs the rules for settings and for conversations against a store whose conversation ids are of another
        /// type than its settings ids, and the rules for decisions and agents' own settings when it keeps those too.
        /// It writes under all four ids, so give it ids that nothing else uses, in a database for tests.
        /// </summary>
        /// <typeparam name="TConversationId">The type of the store's conversation ids.</typeparam>
        /// <param name="settingsId">A settings id nothing is stored under yet.</param>
        /// <param name="otherSettingsId">
        /// A second such settings id. What the check stores under it must stay as it is while the check writes under
        /// the first, which tells a store that forgets the id.
        /// </param>
        /// <param name="conversationId">A conversation id nothing is stored under yet.</param>
        /// <param name="otherConversationId">A second such conversation id, as for the settings ids.</param>
        /// <param name="ct">Cancels the check.</param>
        /// <returns>A task that completes once the store passed.</returns>
        /// <exception cref="InvalidOperationException">
        /// The store broke a rule, something is stored under one of the ids already, or the store keeps no
        /// conversations under ids of <typeparamref name="TConversationId"/>; the message says which.
        /// </exception>
        /// <exception cref="ArgumentException">The two settings ids or the two conversation ids are equal.</exception>
        public Task VerifyAsync<TConversationId>(
            TId settingsId,
            TId otherSettingsId,
            TConversationId conversationId,
            TConversationId otherConversationId,
            CancellationToken ct = default)
            where TConversationId : notnull =>
            VerifyEveryInterfaceAsync(
                store,
                store,
                (settingsId, otherSettingsId),
                store as IConversationStore<TConversationId> ?? throw NoConversations<TConversationId>(store),
                (conversationId, otherConversationId),
                ct);
    }

    extension<TId>(IConversationStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for conversations against this store, and the rules for settings, decisions and agents' own
        /// settings too when it keeps those as well. It writes under both ids, so give it two that nothing else uses,
        /// in a database for tests.
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
        [OverloadResolutionPriority(1)]
        public Task VerifyAsync(TId id, TId otherId, CancellationToken ct = default) =>
            VerifyEveryInterfaceAsync(store, store as ISettingsStore<TId>, (id, otherId), store, (id, otherId), ct);
    }

    extension<TId>(IDecisionStore<TId> store) where TId : notnull
    {
        /// <summary>
        /// Runs the rules for decisions against this store, and the rules for settings, conversations and agents' own
        /// settings too when it keeps those as well. It writes under both ids, so give it two that nothing else uses,
        /// in a database for tests.
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
            VerifyEveryInterfaceAsync(
                store,
                store as ISettingsStore<TId>,
                (id, otherId),
                store as IConversationStore<TId>,
                (id, otherId),
                ct);
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

    // D72: settings and conversations may have ids of two types, each with a second id of its type
    // D66: a store that keeps agents' own settings too is checked under kinds named after the id, since they take no
    // id, and its agents' own documents must stay apart from an id's of the same kind
    private static async Task VerifyEveryInterfaceAsync<TSettingsId, TConversationId>(
        object store,
        ISettingsStore<TSettingsId>? settings,
        (TSettingsId Id, TSettingsId Other) settingsIds,
        IConversationStore<TConversationId>? conversations,
        (TConversationId Id, TConversationId Other) conversationIds,
        CancellationToken ct)
        where TSettingsId : notnull
        where TConversationId : notnull
    {
        if (EqualityComparer<TSettingsId>.Default.Equals(settingsIds.Id, settingsIds.Other)
            || EqualityComparer<TConversationId>.Default.Equals(conversationIds.Id, conversationIds.Other))
        {
            throw new ArgumentException(
                "VerifyAsync needs two different ids: what it stores under the second shows whether the store tells "
                + "ids apart.");
        }

        if (settings is not null)
            await VerifySettingsAsync(settings, settingsIds.Id, settingsIds.Other, ct).ConfigureAwait(false);

        if (conversations is not null)
        {
            await VerifyConversationsAsync(conversations, conversationIds.Id, conversationIds.Other, ct)
                .ConfigureAwait(false);
        }

        // D79: a conversation's decisions are under its id, so a store of them is checked with the conversation ids
        if (store is IDecisionStore<TConversationId> decisions)
            await VerifyDecisionsAsync(decisions, conversationIds.Id, conversationIds.Other, ct).ConfigureAwait(false);

        if (store is not IAgentSettingsStore agents) return;

        var kind = settings is null ? $"{Kind}_{conversationIds.Id}" : $"{Kind}_{settingsIds.Id}";

        await VerifyAgentDocumentsAsync(agents, kind, ct).ConfigureAwait(false);
        if (settings is not null)
            await VerifyApartAsync(settings, agents, settingsIds.Id, $"{kind}_apart", ct).ConfigureAwait(false);
    }

    private static InvalidOperationException NoConversations<TConversationId>(object store) =>
        new(
            $"{store.GetType().Name} is no IConversationStore<{TypeNames.Of<TConversationId>()}>: pass conversation "
            + "ids of the type the store keeps, or only its settings ids.");

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

        var saved = await settings.SaveAsync(id, kind, idJson, 0, ct).ConfigureAwait(false);

        if (await agents.LoadAsync(kind, ct).ConfigureAwait(false) is not null)
            throw Broken($"An agent's own load returned a document saved under an id: {apart}");

        long own;

        try
        {
            own = await agents.SaveAsync(kind, agentJson, 0, ct).ConfigureAwait(false);
        }
        catch (StoreConflictException exception)
        {
            throw Broken(
                $"A first save of an agent's own document conflicted with one under an id: {apart}",
                exception);
        }

        if (await settings.LoadAsync(id, kind, ct).ConfigureAwait(false) != new StoredDocument(idJson, saved))
            throw Broken($"Saving an agent's own document replaced the one under an id: {apart}");

        if (await agents.LoadAsync(kind, ct).ConfigureAwait(false) != new StoredDocument(agentJson, own))
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
            if (await store.LoadAsync(taken, Kind, ct).ConfigureAwait(false) is not null)
                throw new InvalidOperationException(Taken(taken));
        }

        long other;

        try
        {
            other = await store.SaveAsync(otherId, Kind, otherJson, 0, ct).ConfigureAwait(false);
        }
        catch (StoreConflictException exception)
        {
            throw Broken("A first save at version 0 threw a StoreConflictException.", exception);
        }

        if (await store.LoadAsync(id, Kind, ct).ConfigureAwait(false) is not null)
            throw Broken($"LoadAsync returned a document saved under another id: {ById}");

        await VerifyDocumentsAsync(
            kind => store.LoadAsync(id, kind, ct),
            (kind, json, loadedVersion) => store.SaveAsync(id, kind, json, loadedVersion, ct),
            new(Kind, Taken(id), " under the same id", "id and kind together")).ConfigureAwait(false);

        if (await store.LoadAsync(otherId, Kind, ct).ConfigureAwait(false) != new StoredDocument(otherJson, other))
            throw Broken($"Saving a document under one id changed the one under another id: {ById}");
    }

    private static string Taken<TId>(TId id) => $"LoadAsync returned a document for {id}: give VerifyAsync a new id.";

    // D79: a conversation's decisions, one document keyed by the conversation alone and versioned as settings are,
    // beside another conversation's, which the saves must leave alone
    private static async Task VerifyDecisionsAsync<TId>(
        IDecisionStore<TId> store,
        TId conversation,
        TId other,
        CancellationToken ct)
        where TId : notnull
    {
        const string otherJson = """{"of":"another conversation"}""";
        const string byConversation = "key decisions by the conversation id.";

        foreach (var taken in (TId[])[conversation, other])
        {
            if (await store.LoadDecisionsAsync(taken, ct).ConfigureAwait(false) is not null)
                throw new InvalidOperationException(TakenDecisions(taken));
        }

        long elsewhere;

        try
        {
            elsewhere = await store.SaveDecisionsAsync(other, otherJson, 0, ct).ConfigureAwait(false);
        }
        catch (StoreConflictException exception)
        {
            throw Broken("A first save of decisions at version 0 threw a StoreConflictException.", exception);
        }

        if (await store.LoadDecisionsAsync(conversation, ct).ConfigureAwait(false) is not null)
            throw Broken($"LoadDecisionsAsync returned a document saved under another conversation: {byConversation}");

        await VerifyDocumentsAsync(
            _ => store.LoadDecisionsAsync(conversation, ct),
            (_, json, loadedVersion) => store.SaveDecisionsAsync(conversation, json, loadedVersion, ct),
            new(Kind, TakenDecisions(conversation), "", "the conversation id")
            {
                IsOnlyKind = true,
                Load = "LoadDecisionsAsync",
                Save = "SaveDecisionsAsync"
            }).ConfigureAwait(false);

        if (await store.LoadDecisionsAsync(other, ct).ConfigureAwait(false) != new StoredDocument(otherJson, elsewhere))
            throw Broken($"Saving decisions under one conversation changed those under another: {byConversation}");
    }

    private static string TakenDecisions<TId>(TId conversation) =>
        $"LoadDecisionsAsync returned a document for {conversation}: give VerifyAsync a new id.";

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

        if (await load(keys.Kind).ConfigureAwait(false) is not null) throw new InvalidOperationException(keys.Taken);

        const string missing =
            "A save at a version other than 0 of a document that is not stored did not throw a "
            + "StoreConflictException: update only a row that holds the version.";

        // A document its key holds alone has no kind to leave unstored, so the rule runs before its first save
        if (keys.IsOnlyKind) await EnsureConflictAsync(keys.Kind, 1, null, missing).ConfigureAwait(false);

        var first = await SaveAsync(
                keys.Kind,
                firstJson,
                0,
                $"A first save at version 0 threw a StoreConflictException: key documents by {keys.Key}.")
            .ConfigureAwait(false);

        await EnsureStoredAsync(
            keys.Kind,
            new(firstJson, first),
            $"{keys.Load} did not return the last save and version.").ConfigureAwait(false);

        if (!keys.IsOnlyKind)
        {
            var other = await SaveAsync(
                    keys.OtherKind,
                    otherJson,
                    0,
                    $"Saving a document of another kind{keys.Scope} conflicted: key documents by {keys.Key}.")
                .ConfigureAwait(false);

            await EnsureStoredAsync(
                    keys.Kind,
                    new(firstJson, first),
                    $"Saving a document of another kind{keys.Scope} replaced this one: key documents by {keys.Key}.")
                .ConfigureAwait(false);

            await EnsureStoredAsync(
                keys.OtherKind,
                new(otherJson, other),
                $"{keys.Load} returned another kind's document: load by {keys.Key}.").ConfigureAwait(false);
        }

        await EnsureConflictAsync(
                keys.Kind,
                0,
                new(firstJson, first),
                "A save at version 0 of a document stored already did not throw a StoreConflictException.")
            .ConfigureAwait(false);

        var second = await SaveAsync(
            keys.Kind,
            secondJson,
            first,
            "A save with the version the last save returned threw a StoreConflictException.").ConfigureAwait(false);

        if (second == first) throw Broken($"{keys.Save} returned the version the document had before the save.");

        await EnsureConflictAsync(
                keys.Kind,
                first,
                new(secondJson, second),
                "A save with a version another save replaced did not throw a StoreConflictException.")
            .ConfigureAwait(false);

        // A document deleted since the caller loaded it: an update that inserts the missing row brings it back
        if (!keys.IsOnlyKind) await EnsureConflictAsync(keys.MissingKind, second, null, missing).ConfigureAwait(false);

        async Task<long> SaveAsync(string kind, string json, long loadedVersion, string conflictRule)
        {
            try
            {
                var saved = await save(kind, json, loadedVersion).ConfigureAwait(false);
                if (saved == 0)
                    throw Broken($"{keys.Save} returned version 0, which tells Typeshift nothing is stored.");

                return saved;
            }
            catch (StoreConflictException exception)
            {
                throw Broken(conflictRule, exception);
            }
        }

        async Task EnsureStoredAsync(string kind, StoredDocument? expected, string rule)
        {
            if (await load(kind).ConfigureAwait(false) != expected) throw Broken(rule);
        }

        async Task EnsureConflictAsync(string kind, long loadedVersion, StoredDocument? stored, string rule)
        {
            try
            {
                await save(kind, """{"step":0}""", loadedVersion).ConfigureAwait(false);
            }
            catch (StoreConflictException)
            {
                await EnsureStoredAsync(
                        kind,
                        stored,
                        "A save that conflicted changed the document: check the version and write in one statement.")
                    .ConfigureAwait(false);

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
    // on, as runs do, beside another conversation that must load as it was and holds the same number. Last, a second
    // run's entry under a number the conversation holds, which a store that says it refuses one must refuse (D50)
    private static async Task VerifyConversationsAsync<TId>(
        IConversationStore<TId> store,
        TId conversation,
        TId other,
        CancellationToken ct)
        where TId : notnull
    {
        foreach (var taken in (TId[])[conversation, other])
        {
            if ((await store.LoadConversationAsync(taken, ct).ConfigureAwait(false)).Count > 0)
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

        ConversationEntry[] elsewhere = [Entry(0, ChatRole.User) with { Sequence = entries[0].Sequence }];
        var second = Entry(7, ChatRole.User) with { Sequence = entries[^1].Sequence };
        HashSet<string> appended = [.. entries.Concat([.. elsewhere, second]).Select(entry => entry.Serialize())];

        const string order =
            "LoadConversationAsync did not return the entries in the order appended, oldest first: order them by a "
            + "number the store assigns as it appends, never by their times, which tie and run backwards.";

        await AppendToAsync(other, elsewhere[0]).ConfigureAwait(false);

        if ((await store.LoadConversationAsync(conversation, ct).ConfigureAwait(false)).Count > 0)
        {
            throw Broken(
                "LoadConversationAsync returned entries appended to another conversation: key entries by the "
                + "conversation id.");
        }

        await AppendAsync(entries[..2]).ConfigureAwait(false);
        EnsureSame(await store.LoadConversationAsync(conversation, ct).ConfigureAwait(false), entries[..2], order);

        EnsureSame(
            await store.LoadConversationSinceCompactionAsync(conversation, ct).ConfigureAwait(false),
            entries[..2],
            "LoadConversationSinceCompactionAsync did not return every entry of a conversation with no summary.");

        await AppendAsync(entries[2..]).ConfigureAwait(false);
        EnsureSame(await store.LoadConversationAsync(conversation, ct).ConfigureAwait(false), entries, order);

        EnsureSame(
            await store.LoadConversationSinceCompactionAsync(conversation, ct).ConfigureAwait(false),
            entries[4..],
            "LoadConversationSinceCompactionAsync did not start at the last summary.");

        EnsureSame(
            await store.LoadConversationSinceCompactionAsync(other, ct).ConfigureAwait(false),
            elsewhere,
            "LoadConversationSinceCompactionAsync started a conversation at another conversation's summary: look for "
            + "the last summary of the conversation it is given.");

        try
        {
            await store.AppendConversationAsync(conversation, second, ct).ConfigureAwait(false);
        }
        catch (StoreConflictException)
        {
            EnsureSame(
                await store.LoadConversationAsync(conversation, ct).ConfigureAwait(false),
                entries,
                "An append the store refused changed the conversation: check the number and write in one statement.");

            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw Broken(
                $"An append under a number its conversation holds threw a {exception.GetType().Name}: refuse it with a "
                + "StoreConflictException, with the database's exception inside, so that the second run stops.",
                exception);
        }

        if (store.RefusesTakenSequences)
        {
            throw Broken(
                "The store says it refuses an entry under a number its conversation holds, and stored one: keep a "
                + "unique key on the conversation and the entry's Sequence, and throw a StoreConflictException when it "
                + "refuses an entry.");
        }

        async Task AppendAsync(ConversationEntry[] steps)
        {
            foreach (var entry in steps)
            {
                await AppendToAsync(conversation, entry).ConfigureAwait(false);
            }
        }

        // A number another conversation holds is no reason to refuse an entry
        async Task AppendToAsync(TId id, ConversationEntry entry)
        {
            try
            {
                await store.AppendConversationAsync(id, entry, ct).ConfigureAwait(false);
            }
            catch (StoreConflictException exception)
            {
                throw Broken(
                    "An append threw a StoreConflictException for a number its conversation does not hold: key the "
                    + "numbers by the conversation and the entry's Sequence together.",
                    exception);
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
    // times of real entries can: a store that orders by them fails. Each step is numbered as a run numbers it
    private static ConversationEntry Entry(int step, ChatRole role) =>
        new(Agent, [new ChatMessage(role, $"Step {step}.")], DateTimeOffset.UnixEpoch.AddSeconds(step * 2 % 3))
        {
            Sequence = step
        };

    private static ConversationEntry RunEnd(int step) =>
        Entry(step, ChatRole.Assistant) with
        {
            EndsRun = true
        };

    private static ConversationEntry Summary(int step) =>
        Entry(step, ChatRole.User) with
        {
            IsSummary = true
        };

    private static InvalidOperationException Broken(string rule, Exception? inner = null) =>
        new($"The store broke a rule Typeshift relies on. {rule}", inner);

    // The kinds a check writes, what it says when the first is taken, and how documents are keyed
    private sealed record DocumentKeys(string Kind, string Taken, string Scope, string Key)
    {
        // A document its key holds alone, as a conversation's decisions, has no other kind beside it
        public bool IsOnlyKind { get; init; }

        // The store's methods, as a broken rule names them
        public string Load { get; init; } = "LoadAsync";

        public string Save { get; init; } = "SaveAsync";

        public string OtherKind => $"{Kind}_other";

        // Never stored by a store that keeps the rules
        public string MissingKind => $"{Kind}_missing";
    }
}
