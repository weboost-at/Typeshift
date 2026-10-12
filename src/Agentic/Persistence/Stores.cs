using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Persistence;

// D26: the store is the persistence feature flag; without one, whatever loads or saves throws and says what to add
internal static class Stores
{
    public static IAgenticStore<TId> Get<TId>(IServiceProvider services) where TId : notnull =>
        services.GetService<IAgenticStore<TId>>()
        ?? throw new InvalidOperationException(
            $"No IAgenticStore<{typeof(TId).Name}> is registered, so nothing can be stored under a "
            + $"{typeof(TId).Name} id. Register one with services.AddAgenticStore<TStore, {typeof(TId).Name}>().");
}
