using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

/// <summary>
/// The registration chain <c>AddTypeshift()</c> starts.
/// </summary>
public sealed class TypeshiftBuilder
{
    private readonly IServiceCollection _services;
    private readonly Action<IServiceCollection, Func<IServiceProvider, object>> _addStore;

    internal TypeshiftBuilder(
        IServiceCollection services,
        Action<IServiceCollection, Func<IServiceProvider, object>> addStore)
    {
        _services = services;
        _addStore = addStore;
    }

    /// <summary>
    /// Registers the store, a singleton, and turns persistence on: stored settings and conversations. Without it,
    /// every call that loads or saves throws, naming this method.
    /// </summary>
    /// <typeparam name="TStore">An <c>ITypeshiftStore</c> of the build's id type (TSH028).</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>()
        where TStore : class
    {
        _services.AddSingleton<TStore>();
        _addStore(_services, provider => provider.GetRequiredService<TStore>());
        return this;
    }

    /// <summary>
    /// Registers the provider that maps model names to clients, scoped.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithChatClientProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IChatClientProvider
    {
        _services.AddScoped<IChatClientProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Publishes the updates of streaming runs live, to <c>ConversationTracker.WatchAsync</c>.
    /// </summary>
    /// <param name="options">Intervals and limits; the defaults when null.</param>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithRealtimeTracking(RealtimeTrackingOptions? options = null)
    {
        _services.AddSingleton(options ?? new RealtimeTrackingOptions());
        return this;
    }

    /// <summary>
    /// Adds a rule that decides tool calls before the host is asked, scoped; any rule that rejects a call wins, then
    /// any that approves it.
    /// </summary>
    /// <typeparam name="TRule">The rule.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithApprovalRule<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRule>()
        where TRule : class, IToolApprovalRule
    {
        _services.AddScoped<IToolApprovalRule, TRule>();
        return this;
    }
}
