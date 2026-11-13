using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// Runs <typeparamref name="TAgent"/> with the services of its scope: its tools and its chat client provider resolve
/// there. Registered as transient by <c>AddTypeshift()</c>: inject it, or resolve it from a scope, never from the root
/// provider. <see cref="TypeshiftRuntime.Agent{TAgent}"/> creates one in a scope of its own, which disposing the host
/// disposes. The generator adds the agent's typed members: <c>RunAsync</c>, <c>RunAsync&lt;T&gt;</c>,
/// <c>RunStreamingAsync</c>, <c>RunStreamingAsync&lt;T&gt;</c>, <c>ResumeAsync</c>, <c>ResumeStreamingAsync</c>,
/// <c>ResolveOptionsAsync</c>, <c>LoadSettingsAsync</c>, <c>SaveSettingsAsync</c> and <c>ConfigureAsync</c>.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
public sealed class AgentHost<TAgent> : IAgentHost, IAsyncDisposable, IDisposable where TAgent : Agent
{
    // D25: the services of the scope it was resolved from, or of the one the runtime created for it (D76)
    private readonly IServiceProvider _services;

    // D76: only a host the runtime created owns its scope; one the container resolved leaves it to the container
    private readonly AsyncServiceScope? _scope;

    // D72: the ids WithSettingsId and WithConversationId gave, each with its type
    private StoreId? _settingsId;
    private StoreId? _conversationId;

    internal AgentHost(TAgent agent, IServiceProvider services)
    {
        Agent = agent;
        _services = services;
    }

    internal AgentHost(TAgent agent, AsyncServiceScope scope) : this(agent, scope.ServiceProvider) => _scope = scope;

    // D76: built rather than resolved where the library makes a host, since a scope keeps every disposable the
    // container builds until it ends
    internal static AgentHost<TAgent> In(IServiceProvider services) =>
        new(services.GetRequiredService<TAgent>(), services);

    /// <summary>
    /// The agent as its code declares it.
    /// </summary>
    public TAgent Agent { get; }

    /// <summary>
    /// A host whose runs apply the settings stored under an id, over the agent's own, and whose settings members load,
    /// save and configure them. The id's type picks the store: an <see cref="ISettingsStore{TId}"/> of that type.
    /// </summary>
    /// <param name="id">The id of the stored settings.</param>
    /// <typeparam name="TId">The id's type.</typeparam>
    /// <returns>
    /// A host with the id, in this host's scope; this one keeps the ids it had. Disposing it leaves the scope to this
    /// host.
    /// </returns>
    public AgentHost<TAgent> WithSettingsId<TId>(TId id) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(id);
        return Copy(new StoreId<TId>(id), _conversationId);
    }

    /// <summary>
    /// A host whose runs resume a conversation and store what they add to it. The id's type picks the store: an
    /// <see cref="IConversationStore{TId}"/> of that type.
    /// </summary>
    /// <param name="id">The conversation's id.</param>
    /// <typeparam name="TId">The id's type.</typeparam>
    /// <returns>
    /// A host with the id, in this host's scope; this one keeps the ids it had. Disposing it leaves the scope to this
    /// host.
    /// </returns>
    public AgentHost<TAgent> WithConversationId<TId>(TId id) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(id);
        return Copy(_settingsId, new StoreId<TId>(id));
    }

    // D72, D76: a copy shares this host's scope but never owns it, so only the host the runtime created disposes it
    private AgentHost<TAgent> Copy(StoreId? settingsId, StoreId? conversationId) =>
        new(Agent, _services)
        {
            _settingsId = settingsId,
            _conversationId = conversationId
        };

    // The settings id WithSettingsId gave, which the settings members use in place of the agent's own settings
    internal StoreId? SettingsId => _settingsId;

    internal StoreId? ConversationId => _conversationId;

    // A resume needs a conversation
    internal StoreId ResumedConversation =>
        _conversationId
        ?? throw new InvalidOperationException(
            "A resume needs a conversation: resume on a host from WithConversationId(id).");

    /// <summary>
    /// The raw path: creates the client of a run whose options <c>ResolveOptionsAsync</c> resolved. It takes the
    /// client from the <see cref="IChatClientProvider{TProviderId}"/> of the provider's type, an
    /// <see cref="IChatClient"/> registered under the options' provider, or the one registered for every agent, and
    /// wraps it in the tool loop.
    /// </summary>
    /// <param name="options">The resolved options; call the client with them.</param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client; disposing it leaves the provider's client alone.</returns>
    /// <exception cref="InvalidOperationException">
    /// No chat client serves the options' provider and model, or the agent, which names no model: no
    /// <c>IChatClientProvider</c> of the provider's type is registered for a provider and a model, and no
    /// <see cref="IChatClient"/> under the provider's key or for every agent.
    /// </exception>
    public async Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)
    {
        var resolved = options as ResolvedChatOptions;
        var provider = resolved is null ? Agent.Provider : resolved.Provider;
        var model = options.ModelId;

        var client = await ChatClients.GetAsync(_services, provider, model, ct)
            ?? throw new InvalidOperationException(ChatClients.NoClient($"the agent '{Agent.Name}'", provider, model));
        var run = resolved?.RunOptions ?? Agent.RunOptions;

        // D29: outermost the conversation, then run middleware, context and approval above the tool loop, the
        // checkpoints, unique call ids (D59) and compaction inside it, and innermost the provider's client,
        // instrumented, behind a wrapper that never disposes it
        var provided = _services.GetRequiredService<InstrumentedClients>().Instrument(client, model);

        var checkpoints = new CheckpointChatClient(new UniqueCallIdsChatClient(new CompactingChatClient(provided)));
        var middleware = _services.GetServices<IToolMiddleware>().ToList();

        var approval = new ApprovalChatClient(Loop(run.MaxToolIterations), Loop);
        var context = new ContextChatClient(approval);

        return new ConversationChatClient(new RunMiddlewareChatClient(context), Agent, _services);

        // D32: a tool loop per call with that call's iteration limit, so concurrent calls never share one
        IChatClient Loop(int iterations)
        {
            var loop = new FunctionInvokingChatClient(checkpoints, _services.GetService<ILoggerFactory>(), _services)
            {
                MaximumIterationsPerRequest = iterations,
                MaximumConsecutiveErrorsPerRequest = run.MaxConsecutiveToolErrors,
                AllowConcurrentInvocation = run.ConcurrentToolCalls,

                // D53: the model fixes a failed call only when it reads why it failed
                IncludeDetailedErrors = true,

                // An approved plan runs whatever mode the run continues in
                AdditionalTools = [ExitPlanModeFunction.Instance]
            };

            if (middleware.Count > 0)
                loop.FunctionInvoker = (call, ct) => ToolMiddleware.InvokeAsync(middleware, 0, call, ct);

            return new AuthorNameChatClient(loop, Agent.Name);
        }
    }

    /// <summary>
    /// Disposes the scope of a host <see cref="TypeshiftRuntime.Agent{TAgent}"/> created, and with it the scoped
    /// services its runs resolved. A host your app's container resolved, injected into a request for example, owns
    /// nothing, and disposing it leaves its scope alone. So does a copy <c>WithSettingsId</c> or
    /// <c>WithConversationId</c> made, which works only as long as the host it came from.
    /// </summary>
    /// <returns>The disposal.</returns>
    public ValueTask DisposeAsync() => _scope?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>
    /// Disposes the scope of a host <see cref="TypeshiftRuntime.Agent{TAgent}"/> created. Prefer
    /// <see cref="DisposeAsync"/>: this one throws when a scoped service disposes only asynchronously.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A service of the scope implements only <see cref="IAsyncDisposable"/>.
    /// </exception>
    public void Dispose() => _scope?.Dispose();

    // D27, D66: the agent's code, its own stored settings, those under the settings id, then the run's; values a layer
    // leaves null keep the one below
    internal async Task<ChatOptions> ResolveOptionsCoreAsync(
        StoreId? settingsId,
        object? settings,
        CancellationToken ct)
    {
        var declaration = Agent.GeneratedDeclaration;
        var loads = new[]
        {
            Store.KeepsAgentSettings ? declaration.LoadAgentSettingsAsync(Store, ct) : Task.FromResult<object?>(null),
            settingsId is null ? Task.FromResult<object?>(null) : declaration.LoadSettingsAsync(Store, settingsId, ct)
        };
        object?[] loaded = [.. await Task.WhenAll(loads), settings];
        IReadOnlyList<object> layers = [.. loaded.OfType<object>()];

        var options = new ResolvedChatOptions(Agent.ChatOptions)
        {
            Instructions = Agent.Instructions,
            Provider = Agent.Provider
        };
        var run = Agent.RunOptions;

        foreach (var layer in layers)
        {
            run = declaration.ApplySettings(options, run, layer);
        }

        var tools = declaration.CreateTools(Agent, layers, _services.GetRequiredService<ToolJson>());

        // D33, D35: the sub-agents and the MCP servers' tools after the toolsets'
        var agents = Agent.SubAgents.Select(use => new SubAgentFunction(use, use.Host(_services).Agent));
        List<AITool> own = [.. tools, .. agents, .. options.Tools ?? []];

        options.Tools = [.. own, .. await McpToolsAsync(own, ct)];
        options.RunOptions = run;
        options.RunSetsMode = settings is not null && SetsMode(declaration, settings);
        options.SettingsId = settingsId;

        // The raw path's conversation, which a run replaces with its own
        options.Conversation = _conversationId;

        return options;
    }

    // A run without input resumes the conversation's interrupted run (D50)
    internal Task<ChatResponse> RunCoreAsync(
        ChatMessage? input,
        StoreId? settingsId,
        StoreId? conversationId,
        object? settings,
        CancellationToken ct) =>
        RunAsync(input is null ? [] : [input], settingsId, conversationId, settings, null, null, null, ct);

    internal async Task<ChatResponse<TResult>> RunCoreAsync<TResult>(
        ChatMessage input,
        StoreId? settingsId,
        StoreId? conversationId,
        object? settings,
        CancellationToken ct)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();
        var response = await RunAsync([input], settingsId, conversationId, settings, null, null, result.Format, ct);

        return new ChatResponse<TResult>(response, result.SerializerOptions);
    }

    // D64: the typed answer's format, as RunCoreAsync<TResult> asks for it, over a streaming run
    internal StreamingRun<TResult> RunStreamingCoreAsync<TResult>(
        ChatMessage input,
        StoreId? settingsId,
        StoreId? conversationId,
        object? settings,
        CancellationToken ct)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();
        return new StreamingRun<TResult>(
            RunStreamingAsync([input], settingsId, conversationId, settings, null, result.Format, ct),
            result.SerializerOptions);
    }

    internal IAsyncEnumerable<ChatResponseUpdate> RunStreamingCoreAsync(
        ChatMessage? input,
        StoreId? settingsId,
        StoreId? conversationId,
        object? settings,
        CancellationToken ct) =>
        RunStreamingAsync(input is null ? [] : [input], settingsId, conversationId, settings, null, null, ct);

    internal StoreAccess Store => _services.GetRequiredService<StoreAccess>();

    Agent IAgentHost.Agent => Agent;

    Task<ChatResponse> IAgentHost.RunAsync(
        IEnumerable<ChatMessage> messages,
        StoreId? settingsId,
        StoreId? conversation,
        AgentRun? caller,
        RunClaim? claim,
        ChatResponseFormat? format,
        CancellationToken ct) =>
        RunAsync(messages, settingsId, conversation, null, caller, claim, format, ct);

    IAsyncEnumerable<ChatResponseUpdate> IAgentHost.RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        StoreId? settingsId,
        StoreId? conversation,
        RunClaim? claim,
        ChatResponseFormat? format,
        CancellationToken ct) =>
        RunStreamingAsync(messages, settingsId, conversation, null, claim, format, ct);

    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        StoreId? settingsId,
        StoreId? conversation,
        object? settings,
        AgentRun? caller,
        RunClaim? claim,
        ChatResponseFormat? format,
        CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversation, settings, caller, ct);
        options.Claim = claim;
        AskFor(options, format);
        using var client = await CreateChatClientAsync(options, ct);

        return await client.GetResponseAsync(messages, options, ct);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        StoreId? settingsId,
        StoreId? conversation,
        object? settings,
        RunClaim? claim,
        ChatResponseFormat? format,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversation, settings, null, ct);
        options.Claim = claim;
        AskFor(options, format);
        using var client = await CreateChatClientAsync(options, ct);

        await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct))
        {
            yield return update;
        }
    }

    // D35: a server's tool named like one the agent offers would leave the model one of them, unseen
    private async Task<List<AITool>> McpToolsAsync(IEnumerable<AITool> own, CancellationToken ct)
    {
        if (Agent.McpServers.Count == 0) return [];

        var provider = _services.GetService<IMcpToolProvider>()
            ?? throw new InvalidOperationException(
                $"The agent '{Agent.Name}' uses MCP servers, and no IMcpToolProvider is registered: call "
                + "WithMcpToolProvider<T>() after AddTypeshift().");

        var names = own.Select(tool => tool.Name).ToHashSet();
        var offered = new List<AITool>();

        foreach (var use in Agent.McpServers)
        {
            foreach (var tool in await provider.GetToolsAsync(use.Server, ct))
            {
                if (!names.Add(tool.Name))
                {
                    throw new InvalidOperationException(
                        $"The MCP server '{use.Server}' offers a tool '{tool.Name}', which the agent '{Agent.Name}' "
                        + "already offers: rename one of them, or leave the server out.");
                }

                offered.Add(use.Offer(tool));
            }
        }

        return offered;
    }

    // D64: a typed run's format, which an agent handed the run or answering in its place keeps
    private static void AskFor(ResolvedChatOptions options, ChatResponseFormat? format)
    {
        if (format is null) return;

        options.ResponseFormat = format;
        options.ResultFormat = format;
    }

    // A layer sets the mode when it moves two different modes to the same one
    private static bool SetsMode(AgentDeclaration declaration, object settings) =>
        declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Default }, settings).Mode
        == declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Plan }, settings).Mode;

    // A sub-agent's run goes in its caller's mode (D33)
    private async Task<ResolvedChatOptions> ResolveRunAsync(
        StoreId? settingsId,
        StoreId? conversation,
        object? settings,
        AgentRun? caller,
        CancellationToken ct)
    {
        var options = (ResolvedChatOptions)await ResolveOptionsCoreAsync(settingsId, settings, ct);
        options.Conversation = conversation;
        options.HostRun = true;

        if (caller is not null)
        {
            options.RunOptions = options.RunOptions with { Mode = caller.Mode };
            options.Caller = caller;
        }

        return options;
    }
}
