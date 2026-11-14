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
/// <c>RunStreamingAsync</c>, <c>RunStreamingAsync&lt;T&gt;</c>, <c>AnswerAsync</c>, <c>AnswerAsync&lt;T&gt;</c>,
/// <c>AnswerStreamingAsync</c>, <c>AnswerStreamingAsync&lt;T&gt;</c>, <c>ResumeAsync</c>,
/// <c>ResumeStreamingAsync</c>, <c>ResolveOptionsAsync</c>, <c>LoadSettingsAsync</c>, <c>SaveSettingsAsync</c> and
/// <c>ConfigureAsync</c>.
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

    // D25: the host the container resolves. From the root it would run every customer on one instance of each scoped
    // service, where scopes are not validated, and the root would keep it until the app stops, so it is refused.
    // The root hands a factory the provider the runtime holds, a scope another
    internal static AgentHost<TAgent> Resolved(IServiceProvider services) =>
        ReferenceEquals(services, services.GetRequiredService<TypeshiftRuntime>().Services)
            ? throw new InvalidOperationException(
                $"AgentHost<{typeof(TAgent).Name}> was resolved from the root provider, as a host injected into a "
                + "singleton is. Its runs would share one instance of each scoped service, such as the chat client "
                + "provider of one customer, and the root would keep every host until the app stops: inject "
                + $"TypeshiftRuntime and call Agent<{typeof(TAgent).Name}>() for each unit of work, or resolve the "
                + "host from a scope.")
            : In(services);

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

    private StoreId ResumedConversation =>
        _conversationId
        ?? throw new InvalidOperationException(
            "A resume needs a conversation: resume on a host from WithConversationId(id).");

    // D80: decisions belong to a conversation
    private StoreId DecidedConversation =>
        _conversationId
        ?? throw new InvalidOperationException(
            "Decisions belong to a conversation: call this on a host from WithConversationId(id).");

#pragma warning disable RS0030 // the fallback for a host that registered no TimeProvider
    private DecisionLedger Ledger => new(Store, _services.GetService<TimeProvider>() ?? TimeProvider.System);
#pragma warning restore RS0030

    /// <summary>
    /// Loads what the host's conversation, from <c>WithConversationId</c>, waits for: the decisions your app has not
    /// answered yet, whether the answered ones are ready to be carried out, the tools approved for the conversation
    /// and its mode. It reads the conversation's decisions and none of its history, and starts no run.
    /// </summary>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The decisions; none open for a conversation that never asked.</returns>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public async Task<ConversationDecisions> GetDecisionsAsync(CancellationToken ct = default) =>
        (await Ledger.LoadAsync(DecidedConversation, ct)).ToDecisions();

    /// <summary>
    /// Records your app's answers to decisions of the host's conversation, by their ids, and starts no run:
    /// <c>ResumeAsync</c> continues once every decision is answered, and the generated <c>AnswerAsync</c> does both.
    /// A decision may be answered alone, and the same answer again changes nothing.
    /// </summary>
    /// <param name="answers">The answers, at most one for each decision.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The conversation's decisions after the answers.</returns>
    /// <exception cref="DecisionClosedException">
    /// An answer names a decision that is not open; nothing of the call is recorded.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An answer does not fit its decision, one decision is answered twice, or there is no answer; nothing is
    /// recorded.
    /// </exception>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public async Task<ConversationDecisions> RecordAnswersAsync(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(answers);

        return (await Ledger.RecordAsync(DecidedConversation, answers, ct)).ToDecisions();
    }

    /// <summary>
    /// Withdraws what the host's conversation, from <c>WithConversationId</c>, waits for: every decision that is
    /// open, and every answer no run has begun to carry out. None of the calls runs. The conversation's next run,
    /// with new input or from <c>ResumeAsync</c>, tells the model so, and an answer to a withdrawn decision throws
    /// <see cref="DecisionClosedException"/>. Nothing changes when nothing waits. It starts no run.
    /// </summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The conversation's decisions after the withdrawal, none of them open.</returns>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public async Task<ConversationDecisions> CancelDecisionsAsync(CancellationToken ct = default) =>
        (await Ledger.CancelAsync(DecidedConversation, ct)).ToDecisions();

    /// <summary>
    /// Switches the mode of the host's conversation, from <c>WithConversationId</c>. Its next run is in
    /// <paramref name="mode"/>, whichever agent answers it, and so are the runs after it, until a run's settings, an
    /// approved plan or this method names another mode. It is the way back from auto mode, and needs no configurable
    /// <c>Mode</c>. It starts no run, and a run going on stays in the mode it began in.
    /// </summary>
    /// <param name="mode">The mode the conversation's next runs are in.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The save.</returns>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, or stores are registered and none keeps the decisions of its id type.
    /// </exception>
    public async Task SetModeAsync(AgentMode mode, CancellationToken ct = default) =>
        await Ledger.SetModeAsync(DecidedConversation, mode, ct);

    /// <summary>
    /// Withdraws a standing approval of the host's conversation, from <c>WithConversationId</c>: a tool your app
    /// approved for the conversation asks again, from the conversation's next run on. A run going on keeps the
    /// approvals it began with. Nothing changes when the conversation holds no such approval.
    /// </summary>
    /// <param name="tool">The tool's name, as <see cref="StandingApproval.Tool"/> lists it.</param>
    /// <param name="server">
    /// The MCP server that offers the tool, as <see cref="StandingApproval.Server"/> lists it; null for every other
    /// tool.
    /// </param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The save.</returns>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public async Task RevokeStandingApprovalAsync(string tool, string? server = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        await Ledger.RevokeAsync(DecidedConversation, tool, server, ct);
    }

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

        // D29: outermost the conversation, then run middleware, context and the mode above the tool loop, the gate
        // (D81), the checkpoints, unique call ids (D59) and compaction inside it, and innermost the provider's
        // client, instrumented, behind a wrapper that never disposes it
        var provided = _services.GetRequiredService<InstrumentedClients>().Instrument(client, model);

        var checkpoints = new CheckpointChatClient(new UniqueCallIdsChatClient(new CompactingChatClient(provided)));
        var decisions = new DecisionChatClient(checkpoints);
        var loggers = _services.GetService<ILoggerFactory>();
        var logger = LogEvents.For(loggers);
        var gate = new ToolGate([.. _services.GetServices<IToolMiddleware>()], logger);

        LogEvents.RunResolved(
            logger,
            Agent.Name,
            model,
            provider,
            client.GetType().Name,
            resolved?.Layers ?? "the options it was given");

        var mode = new ModeChatClient(Loop(run.MaxToolIterations), Loop);
        var context = new ContextChatClient(mode);

        return new ConversationChatClient(new RunMiddlewareChatClient(context), Agent, _services);

        // D32: a tool loop per call with that call's iteration limit, so concurrent calls never share one
        IChatClient Loop(int iterations)
        {
            var loop = new ToolLoop(decisions, loggers, _services)
            {
                MaximumIterationsPerRequest = iterations,
                MaximumConsecutiveErrorsPerRequest = run.MaxConsecutiveToolErrors,
                AllowConcurrentInvocation = run.ConcurrentToolCalls,

                // D53: the model fixes a failed call only when it reads why it failed, which the loop passes on for
                // a message written for the model
                IncludeDetailedErrors = true,

                // The call of an answered plan or question resolves whatever the run that continues offers
                AdditionalTools = [ExitPlanModeFunction.Instance, AskUserFunction.Instance],

                // D81: every call goes through its verdict, then the tool middleware (D30)
                FunctionInvoker = gate.InvokeAsync
            };

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

        string?[] applied =
        [
            "its code",
            loaded[0] is null ? null : "its stored settings",
            loaded[1] is null ? null : $"settings id {settingsId}",
            settings is null ? null : "the run"
        ];

        options.Layers = string.Join(", ", applied.OfType<string>());

        var tools = declaration.CreateTools(Agent, layers, _services.GetRequiredService<ToolJson>());

        // D33, D35: the sub-agents and the MCP servers' tools after the toolsets'
        var agents = Agent.SubAgents.Select(use => new SubAgentFunction(use, use.Host(_services).Agent));
        List<AITool> own = [.. tools, .. agents, .. options.Tools ?? []];

        options.Tools = [.. own, .. await McpToolsAsync(own, ct)];
        options.RunOptions = run;
        options.RunSetsMode = NamedMode(settings) is not null;
        options.SettingsId = settingsId;

        // The raw path's conversation, which a run replaces with its own
        options.Conversation = _conversationId;

        return options;
    }

    // A run without input resumes the conversation's interrupted run (D50)
    internal Task<ChatResponse> RunCoreAsync(ChatMessage? input, object? settings, CancellationToken ct) =>
        RunAsync(input is null ? [] : [input], OwnRun(Conversation(input)), settings, ct);

    // A resume needs a conversation
    private StoreId? Conversation(ChatMessage? input) => input is null ? ResumedConversation : _conversationId;

    // A run the host's own members start is handed nothing but the host's ids
    private RunHandover OwnRun(StoreId? conversation, ChatResponseFormat? format = null) =>
        new(_settingsId, conversation, Claim: null, format);

    internal async Task<ChatResponse<TResult>> RunCoreAsync<TResult>(
        ChatMessage? input,
        object? settings,
        CancellationToken ct)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();
        var response = await RunAsync(
            input is null ? [] : [input],
            OwnRun(Conversation(input), result.Format),
            settings,
            ct);

        return new ChatResponse<TResult>(response, result.SerializerOptions);
    }

    // D64: the typed answer's format, as RunCoreAsync<TResult> asks for it, over a streaming run
    internal StreamingRun<TResult> RunStreamingCoreAsync<TResult>(
        ChatMessage input,
        object? settings,
        CancellationToken ct)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();
        return new StreamingRun<TResult>(
            RunStreamingAsync([input], OwnRun(_conversationId, result.Format), settings, ct),
            result.SerializerOptions);
    }

    // D80: the answers are recorded first, by compare-and-set; the run then continues as a resume does, which calls
    // no model while a decision is left open
    internal async Task<ChatResponse> AnswerCoreAsync(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
    {
        await RecordAnswersAsync(answers, ct);
        return await RunCoreAsync(null, settings, ct);
    }

    internal async Task<ChatResponse<TResult>> AnswerCoreAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
    {
        await RecordAnswersAsync(answers, ct);
        return await RunCoreAsync<TResult>(null, settings, ct);
    }

    internal IAsyncEnumerable<ChatResponseUpdate> AnswerStreamingCoreAsync(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct) =>
        AnswerStreamingAsync(answers, settings, null, ct);

    internal StreamingRun<TResult> AnswerStreamingCoreAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();

        return new StreamingRun<TResult>(
            AnswerStreamingAsync(answers, settings, result.Format, ct),
            result.SerializerOptions);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> AnswerStreamingAsync(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        ChatResponseFormat? format,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await RecordAnswersAsync(answers, ct);

        var resumed = RunStreamingAsync([], OwnRun(ResumedConversation, format), settings, ct);

        await foreach (var update in resumed)
        {
            yield return update;
        }
    }

    internal IAsyncEnumerable<ChatResponseUpdate> RunStreamingCoreAsync(
        ChatMessage? input,
        object? settings,
        CancellationToken ct) =>
        RunStreamingAsync(input is null ? [] : [input], OwnRun(Conversation(input)), settings, ct);

    internal StoreAccess Store => _services.GetRequiredService<StoreAccess>();

    Agent IAgentHost.Agent => Agent;

    // D34: another agent's run applies none of the run settings of the run that handed it over
    Task<ChatResponse> IAgentHost.RunAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        CancellationToken ct) =>
        RunAsync(messages, handover, settings: null, ct);

    IAsyncEnumerable<ChatResponseUpdate> IAgentHost.RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        CancellationToken ct) =>
        RunStreamingAsync(messages, handover, settings: null, ct);

    // D34: the run takes its conversation before anything resolves, so on a conversation another agent answers,
    // this agent loads no settings, lists no MCP server and asks for no chat client. The claim is the run's once it
    // began, and free again when it never did
    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        object? settings,
        CancellationToken ct)
    {
        var (taken, answering) = await Start.TakeAsync(handover, NamedMode(settings), ct);

        try
        {
            if (answering is not null) return await answering.RunAsync(messages, taken, ct);

            var options = await ResolveRunAsync(taken, settings, ct);
            using var client = await CreateChatClientAsync(options, ct);

            return await client.GetResponseAsync(messages, options, ct);
        }
        finally
        {
            Runs.Release(taken.Claim);
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        object? settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (taken, answering) = await Start.TakeAsync(handover, NamedMode(settings), ct);

        try
        {
            if (answering is not null)
            {
                await foreach (var update in answering.RunStreamingAsync(messages, taken, ct))
                {
                    yield return update;
                }

                yield break;
            }

            var options = await ResolveRunAsync(taken, settings, ct);
            using var client = await CreateChatClientAsync(options, ct);

            await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct))
            {
                yield return update;
            }
        }
        finally
        {
            Runs.Release(taken.Claim);
        }
    }

    private RunStart Start => new(Agent, _services, Ledger);

    private LiveRuns Runs => _services.GetRequiredService<LiveRuns>();

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

    // D52: the mode the run's own settings name. A layer names one when it moves two different modes to the same
    private AgentMode? NamedMode(object? settings)
    {
        if (settings is null) return null;

        var declaration = Agent.GeneratedDeclaration;
        var named = declaration.ApplySettings(new ChatOptions(), new RunOptions(), settings).Mode;
        var onPlan = declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Plan }, settings);

        return named == onPlan.Mode ? named : null;
    }

    // The run's options with what the run was handed: the conversation and the claim on it (D34, D50), a typed run's
    // format, which an agent handed the run or answering in its place keeps (D64), and a sub-agent's caller, in
    // whose mode its run goes (D33)
    private async Task<ResolvedChatOptions> ResolveRunAsync(
        RunHandover handover,
        object? settings,
        CancellationToken ct)
    {
        var options = (ResolvedChatOptions)await ResolveOptionsCoreAsync(handover.SettingsId, settings, ct);
        options.Conversation = handover.Conversation;
        options.Claim = handover.Claim;
        options.Handoffs = handover.Handoffs;
        options.Entries = handover.Entries;
        options.HostRun = true;

        if (handover.Format is { } format)
        {
            options.ResponseFormat = format;
            options.ResultFormat = format;
        }

        if (handover.Caller is { } caller)
        {
            options.RunOptions = options.RunOptions with { Mode = caller.Mode };
            options.Caller = caller;
        }

        return options;
    }
}
