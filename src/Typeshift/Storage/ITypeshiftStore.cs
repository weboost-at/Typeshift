namespace Typeshift;

/// <summary>
/// A store of settings and conversations in one: <see cref="ISettingsStore{TId}"/> and
/// <see cref="IConversationStore{TId}"/>. Each project sees it as <c>ITypeshiftStore</c>, an alias the generator writes
/// for the build's id type (<c>TypeshiftIdType</c>, <see cref="Guid"/> by default).
/// </summary>
/// <typeparam name="TId">
/// The id type of settings and conversations. The library passes on the host's ids, and creates one only for a
/// conversation a run starts with <c>PersistAllConversations</c>.
/// </typeparam>
public interface ITypeshiftStore<TId> : ISettingsStore<TId>, IConversationStore<TId> where TId : notnull;
