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
    private readonly Action<IServiceCollection, Func<IServiceProvider, object>> _addChatClientProvider;

    internal TypeshiftBuilder(
        IServiceCollection services,
        Action<IServiceCollection, Func<IServiceProvider, object>> addStore,
        Action<IServiceCollection, Func<IServiceProvider, object>> addChatClientProvider)
    {
        _services = services;
        _addStore = addStore;
        _addChatClientProvider = addChatClientProvider;
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
    /// Registers the provider that returns the client of a provider's model, scoped.
    /// </summary>
    /// <typeparam name="TProvider">An <c>IChatClientProvider</c> of the build's provider id type (TSH030).</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithChatClientProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class
    {
        _services.AddScoped<TProvider>();
        _addChatClientProvider(_services, provider => provider.GetRequiredService<TProvider>());
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

    /// <summary>
    /// Adds middleware around every run of every agent, scoped; the first registered is the outermost.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithRunMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IRunMiddleware
    {
        _services.AddScoped<IRunMiddleware, TMiddleware>();
        return this;
    }

    /// <summary>
    /// Adds middleware around every tool call of every agent, scoped; the first registered is the outermost.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithToolMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IToolMiddleware
    {
        _services.AddScoped<IToolMiddleware, TMiddleware>();
        return this;
    }

    /// <summary>
    /// Registers what connects the MCP servers agents use, scoped.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithMcpToolProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IMcpToolProvider
    {
        _services.AddScoped<IMcpToolProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Registers the root the file system and shell tools stay within, scoped, so it may differ per customer.
    /// </summary>
    /// <typeparam name="TRoot">The root.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithFileSystemRoot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRoot>()
        where TRoot : class, Toolsets.IFileSystemRoot
    {
        _services.AddScoped<Toolsets.IFileSystemRoot, TRoot>();
        return this;
    }

    /// <summary>
    /// Registers the search engine behind <c>web_search</c>, scoped.
    /// </summary>
    /// <typeparam name="TSearch">The search engine.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithWebSearch<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSearch>()
        where TSearch : class, Toolsets.IWebSearch
    {
        _services.AddScoped<Toolsets.IWebSearch, TSearch>();
        return this;
    }

    /// <summary>
    /// Registers the fetcher behind <c>web_fetch</c>, scoped.
    /// </summary>
    /// <typeparam name="TFetch">The fetcher.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithWebFetch<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFetch>()
        where TFetch : class, Toolsets.IWebFetch
    {
        _services.AddScoped<Toolsets.IWebFetch, TFetch>();
        return this;
    }
}
