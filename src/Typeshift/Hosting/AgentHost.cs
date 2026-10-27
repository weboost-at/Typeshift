using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// Runs <typeparamref name="TAgent"/> with the services of the scope it was resolved from: its tools and its chat
/// client provider resolve there. Registered as transient by <c>AddTypeshift()</c>. The generator adds the agent's
/// typed members (D12): <c>RunAsync</c>, <c>RunAsync&lt;T&gt;</c>, <c>RunStreamingAsync</c>, <c>ResumeAsync</c>,
/// <c>ResumeStreamingAsync</c>, <c>ResolveOptionsAsync</c>, <c>LoadSettingsAsync</c>, <c>SaveSettingsAsync</c> and
/// <c>ConfigureAsync</c>.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
public sealed class AgentHost<TAgent> : IAgentHost where TAgent : Agent
{
    // D25: the scope's services, never a scope of its own
    private readonly IServiceProvider _services;

    internal AgentHost(TAgent agent, IServiceProvider services)
    {
        Agent = agent;
        _services = services;
    }

    /// <summary>
    /// The agent as its code declares it.
    /// </summary>
    public TAgent Agent { get; }

    /// <summary>
    /// The raw path: creates the client of a run whose options <c>ResolveOptionsAsync</c> resolved. It asks the
    /// <see cref="IChatClientProvider{TProviderId}"/> for the options' provider and model and wraps the client in the
    /// tool loop.
    /// </summary>
    /// <param name="options">The resolved options; call the client with them.</param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client; disposing it leaves the provider's client alone.</returns>
    /// <exception cref="InvalidOperationException">
    /// The options name no provider or no model, or no chat client provider is registered.
    /// </exception>
    public async Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)
    {
        var resolved = options as ResolvedChatOptions;
        var provider = resolved is null ? Agent.Provider : resolved.Provider;

        if (provider is null || options.ModelId is not { } model)
            throw new InvalidOperationException(
                $"The agent '{Agent.Name}' has no model: set one with WithChatOptions().WithModel(provider, model), "
                + "or make it configurable and pass a provider and a model in the settings.");

        var client = await _services.GetRequiredService<ChatClients>().GetAsync(_services, provider, model, ct);
        var run = resolved?.RunOptions ?? Agent.RunOptions;

        // D29: outermost the conversation, then run middleware, context and approval above the tool loop, the
        // checkpoints and compaction inside it, and innermost the provider's client, instrumented, behind a wrapper
        // that never disposes it
        var provided = _services.GetRequiredService<InstrumentedClients>().Instrument(client, provider, model);

        var checkpoints = new CheckpointChatClient(new CompactingChatClient(provided));
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

    // D27: the agent's code, its stored settings, then the run's; values a layer leaves null keep the one below
    internal async Task<ChatOptions> ResolveOptionsCoreAsync(object? settingsId, object? settings, CancellationToken ct)
    {
        var declaration = Agent.GeneratedDeclaration;
        var stored = settingsId is null ? null : await declaration.LoadSettingsAsync(Store, settingsId, ct);
        IReadOnlyList<object> layers = [.. new[] { stored, settings }.OfType<object>()];

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

        return options;
    }

    // A run without input resumes the conversation's interrupted run (D50)
    internal Task<ChatResponse> RunCoreAsync(
        ChatMessage? input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct) =>
        RunAsync(input is null ? [] : [input], settingsId, conversationId, settings, null, ct);

    internal async Task<ChatResponse<TResult>> RunCoreAsync<TResult>(
        ChatMessage input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversationId, settings, null, ct);
        using var client = await CreateChatClientAsync(options, ct);
        var json = _services.GetRequiredService<ToolJson>();

        return await client.GetResponseAsync<TResult>([input], json.SerializerOptions, options, cancellationToken: ct);
    }

    internal IAsyncEnumerable<ChatResponseUpdate> RunStreamingCoreAsync(
        ChatMessage? input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct) =>
        RunStreamingAsync(input is null ? [] : [input], settingsId, conversationId, settings, ct);

    internal IStoreAccess Store => _services.GetRequiredService<IStoreAccess>();

    Agent IAgentHost.Agent => Agent;

    Task<ChatResponse> IAgentHost.RunAsync(
        IEnumerable<ChatMessage> messages,
        object? settingsId,
        object? conversation,
        AgentRun? caller,
        CancellationToken ct) =>
        RunAsync(messages, settingsId, conversation, null, caller, ct);

    IAsyncEnumerable<ChatResponseUpdate> IAgentHost.RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        object? settingsId,
        object? conversation,
        CancellationToken ct) =>
        RunStreamingAsync(messages, settingsId, conversation, null, ct);

    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        object? settingsId,
        object? conversation,
        object? settings,
        AgentRun? caller,
        CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversation, settings, caller, ct);
        using var client = await CreateChatClientAsync(options, ct);

        return await client.GetResponseAsync(messages, options, ct);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        object? settingsId,
        object? conversation,
        object? settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversation, settings, null, ct);
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

    // A layer sets the mode when it moves two different modes to the same one
    private static bool SetsMode(AgentDeclaration declaration, object settings) =>
        declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Default }, settings).Mode
        == declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Plan }, settings).Mode;

    // A sub-agent's run goes in its caller's mode (D33)
    private async Task<ResolvedChatOptions> ResolveRunAsync(
        object? settingsId,
        object? conversation,
        object? settings,
        AgentRun? caller,
        CancellationToken ct)
    {
        var options = (ResolvedChatOptions)await ResolveOptionsCoreAsync(settingsId, settings, ct);
        options.Conversation = conversation;

        if (caller is not null)
        {
            options.RunOptions = options.RunOptions with { Mode = caller.Mode };
            options.Caller = caller;
        }

        return options;
    }
}
