using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D38: the store each chain call registered for each store interface. A second store for an interface throws instead
// of taking it over, since the order of two calls would then decide where an app's data goes. The claims live among
// the registrations, as an instance, so every chain AddTypeshift() returns for a service collection sees them all
internal sealed class StoreClaims
{
    private readonly Dictionary<Type, (StoreClaim Store, ServiceDescriptor Registration, bool Yields)> _held = [];

    public static StoreClaims Of(IServiceCollection services)
    {
        var claims = services.Select(service => service.ImplementationInstance).OfType<StoreClaims>().FirstOrDefault();

        if (claims is null) services.AddSingleton(claims = new StoreClaims());

        return claims;
    }

    public void Add(
        IServiceCollection services,
        StoreClaim store,
        IReadOnlyList<Type> contracts,
        bool replace,
        ServiceDescriptor? own,
        Func<Type, ServiceDescriptor> describe)
    {
        var taken = replace ? [] : contracts.Where(contract => IsTaken(contract, store)).ToList();

        if (taken.Count > 0) throw Taken(store, taken);

        var registered = false;

        foreach (var contract in contracts)
        {
            var yields = !replace && store.Yields(contract);

            if (_held.TryGetValue(contract, out var held))
            {
                if (held.Store == store || yields) continue;

                services.Remove(held.Registration);
            }
            else if (yields && services.Any(service => !service.IsKeyedService && service.ServiceType == contract))
            {
                // A store the app registered in the container itself keeps agents' own settings already
                continue;
            }

            var registration = describe(contract);

            services.Add(registration);
            _held[contract] = (store, registration, yields);
            registered = true;
        }

        if (registered && own is not null) services.Add(own);
    }

    // Another store holds the interface, and neither of the two is the memory store keeping agents' own settings
    private bool IsTaken(Type contract, StoreClaim store) =>
        _held.TryGetValue(contract, out var held) && held.Store != store && !held.Yields && !store.Yields(contract);

    // The first store in the way, with every interface it holds that the new one asks for. ITypeshiftStore<TId> is
    // all three of an id type in one, so it is named only when it stands alone
    private InvalidOperationException Taken(StoreClaim store, List<Type> taken)
    {
        var holder = _held[taken.OrderBy(TypeNames.Of, StringComparer.Ordinal).First()].Store;

        var held = taken
            .Where(contract => _held[contract].Store == holder)
            .OrderBy(TypeNames.Of, StringComparer.Ordinal)
            .ToList();

        var parts = held.Where(contract => !IsCombined(contract)).ToList();
        var names = (parts.Count > 0 ? parts : held).Select(TypeNames.Of).ToList();

        var contracts = names.Count == 1
            ? names[0]
            : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";

        return new InvalidOperationException(
            $"{store.Name} and {(holder.Name == store.Name ? $"another {holder.Name}" : holder.Name)} are both "
            + $"registered as {contracts}, and a store interface has one store: remove one of the two calls, or pass "
            + "replace: true to the later one, as in WithStore<T>(replace: true), when its store replaces the other, "
            + "as a test host's does.");
    }

    public static bool IsCombined(Type contract) =>
        contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ITypeshiftStore<>);
}

// A store as a chain call registered it: its declared type, what tells it from another store of that type (the
// instance, the factory or the folder; null for the one the container builds) and the name a message gives it
internal sealed record StoreClaim(Type Store, object? Identity, string Name)
{
    // D69: the memory store keeps agents' own settings only while no other store does, whichever is registered
    // first, so it composes with the file store and with a store of the app's own
    public bool Yields(Type contract) =>
        contract == typeof(IAgentSettingsStore)
        && Store.IsGenericType
        && Store.GetGenericTypeDefinition() == typeof(InMemoryTypeshiftStore<>);
}
