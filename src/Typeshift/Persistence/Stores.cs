using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Persistence;

// D26: the store is the persistence feature flag; without one, whatever loads or saves throws and says what to add
internal static class Stores
{
    public static ITypeshiftStore<TId> Get<TId>(IServiceProvider services) where TId : notnull =>
        services.GetService<ITypeshiftStore<TId>>()
        ?? throw new InvalidOperationException(
            $"No ITypeshiftStore<{typeof(TId).Name}> is registered, so nothing can be stored under a "
            + $"{typeof(TId).Name} id. Register one with services.AddTypeshiftStore<TStore, {typeof(TId).Name}>().");
}
