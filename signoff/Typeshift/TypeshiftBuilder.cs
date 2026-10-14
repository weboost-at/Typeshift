using Typeshift.Toolsets;

namespace Typeshift;

/// <summary>
/// The registration chain <c>AddTypeshift()</c> starts.
/// </summary>
public sealed class TypeshiftBuilder
{
    internal TypeshiftBuilder()
    {
    }

    /// <summary>
    /// Registers the store, a singleton, and turns persistence on: stored settings and conversations. Without it,
    /// every API that loads or saves throws, naming this method.
    /// </summary>
    /// <typeparam name="TStore">An <c>ITypeshiftStore</c> of the build's id type (TSH analyzer checks it).</typeparam>
    public TypeshiftBuilder WithStore<TStore>() where TStore : class => this;

    /// <summary>
    /// Registers the provider that maps model names to clients, scoped.
    /// </summary>
    public TypeshiftBuilder WithChatClientProvider<TProvider>() where TProvider : class, IChatClientProvider => this;

    /// <summary>
    /// Publishes the updates of streaming runs live, to <c>ConversationTracker.WatchAsync</c>.
    /// </summary>
    /// <param name="options">Intervals and limits; the defaults when null.</param>
    public TypeshiftBuilder WithRealtimeTracking(RealtimeTrackingOptions? options = null) => this;

    /// <summary>
    /// Adds middleware around every run, scoped, after those added before it.
    /// </summary>
    public TypeshiftBuilder WithRunMiddleware<TMiddleware>() where TMiddleware : class, IRunMiddleware => this;

    /// <summary>
    /// Adds middleware around every tool call, scoped, after those added before it.
    /// </summary>
    public TypeshiftBuilder WithToolMiddleware<TMiddleware>() where TMiddleware : class, IToolMiddleware => this;

    /// <summary>
    /// Adds a rule that approves tool calls without asking the host, scoped; any rule that approves suffices.
    /// </summary>
    public TypeshiftBuilder WithApprovalRule<TRule>() where TRule : class, IToolApprovalRule => this;

    /// <summary>
    /// Registers what connects the MCP servers agents use, scoped.
    /// </summary>
    public TypeshiftBuilder WithMcpToolProvider<TProvider>() where TProvider : class, IMcpToolProvider => this;

    /// <summary>
    /// Registers the search engine behind <c>web_search</c>, scoped.
    /// </summary>
    public TypeshiftBuilder WithWebSearch<TSearch>() where TSearch : class, IWebSearch => this;

    /// <summary>
    /// Registers the fetcher behind <c>web_fetch</c>, scoped.
    /// </summary>
    public TypeshiftBuilder WithWebFetch<TFetch>() where TFetch : class, IWebFetch => this;

    /// <summary>
    /// Registers the root the file system and shell tools stay within, scoped, so it may differ per customer.
    /// </summary>
    public TypeshiftBuilder WithFileSystemRoot<TRoot>() where TRoot : class, IFileSystemRoot => this;
}

/// <summary>
/// How live updates are published.
/// </summary>
public sealed record RealtimeTrackingOptions
{
    /// <summary>
    /// Text, reasoning and usage updates are merged and published at most this often; others at once.
    /// </summary>
    public TimeSpan PublishInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The updates a watcher may fall behind by; beyond it, the oldest are dropped. A run never waits for a watcher.
    /// </summary>
    public int MaxPendingUpdates { get; init; } = 256;
}
