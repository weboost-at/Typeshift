namespace Agentic.Persistence;

/// <summary>
/// The one store the library persists through: agent settings, tool defaults and conversations. Registering an
/// implementation with <c>AddAgenticStore</c> turns persistence on; without one, every API that needs it throws.
/// </summary>
/// <typeparam name="TId">
/// The id type of your choice, for example <see cref="Guid"/> or <see cref="string"/>. The library never creates
/// ids, it passes on the ones you give it.
/// </typeparam>
public interface IAgenticStore<in TId> where TId : notnull
{
    /// <summary>
    /// Loads the data of type <typeparamref name="TData"/> stored under <paramref name="id"/>.
    /// </summary>
    /// <typeparam name="TData">
    /// The kind of data to load; data of different kinds never share a record, even under the same id.
    /// </typeparam>
    /// <param name="id">The id the data was saved under.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The data, or <see langword="null"/> when nothing of that kind is stored under the id.</returns>
    Task<TData?> LoadAsync<TData>(TId id, CancellationToken ct) where TData : class, IStorable<TData>;

    /// <summary>
    /// Saves <paramref name="data"/> under <paramref name="id"/>, replacing what was stored for that kind and id.
    /// </summary>
    /// <typeparam name="TData">The kind of data to save.</typeparam>
    /// <param name="id">The id to save the data under.</param>
    /// <param name="data">
    /// The data. Store it as <see cref="IStorable{TSelf}.Serialize"/> returns it, or map its typed properties to
    /// columns of your own.
    /// </param>
    /// <param name="ct">Cancels the save.</param>
    Task SaveAsync<TData>(TId id, TData data, CancellationToken ct) where TData : class, IStorable<TData>;
}
