using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Persistence;

internal static class Stores
{
    public static IAgenticStore<TId> Get<TId>(IServiceProvider services) where TId : notnull =>
        services.GetService<IAgenticStore<TId>>()
        ?? throw new InvalidOperationException(
            $"No IAgenticStore<{typeof(TId).Name}> is registered, so nothing can be stored under a "
            + $"{typeof(TId).Name} id. Register one with services.AddAgenticStore<TStore, {typeof(TId).Name}>().");
}
