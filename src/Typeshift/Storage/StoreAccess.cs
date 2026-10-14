using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D11: the library is compiled before the host picks its id type, so runs pass ids as objects to the store of the
// build's type, which the generated registration names
internal interface IStoreAccess
{
    Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct) where TData : class, IStorable<TData>;

    Task<long> SaveAsync<TData>(object id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>;
}

internal sealed class StoreAccess<TId>(IServiceProvider services) : IStoreAccess where TId : notnull
{
    // D43: the store is the persistence switch
    private ITypeshiftStore<TId> Store =>
        services.GetService<ITypeshiftStore<TId>>()
        ?? throw new InvalidOperationException(
            "No store is registered: call WithStore<T>() after AddTypeshift() to load or save settings.");

    public Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Store.LoadAsync<TData>((TId)id, ct);

    public Task<long> SaveAsync<TData>(object id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Store.SaveAsync((TId)id, data, revision, ct);
}
