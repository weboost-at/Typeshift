using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// Runs <typeparamref name="TAgent"/> with the services of the scope it was resolved from: its tools and its chat
/// client provider resolve there. Registered as transient by <c>AddTypeshift()</c>. The generator adds the agent's
/// typed members: <c>RunAsync</c>, <c>RunAsync&lt;T&gt;</c>, <c>RunStreamingAsync</c> and
/// <c>ResolveOptionsAsync</c>.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
public sealed class AgentHost<TAgent> where TAgent : Agent
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
    /// <see cref="IChatClientProvider"/> for the options' model and wraps the client in the tool loop.
    /// </summary>
    /// <param name="options">The resolved options; call the client with them.</param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client; disposing it leaves the provider's client alone.</returns>
    /// <exception cref="InvalidOperationException">The options name no model, or no provider is registered.</exception>
    public async Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)
    {
        var model = options.ModelId
            ?? throw new InvalidOperationException(
                $"The agent '{Agent.Name}' has no model: set one with WithChatOptions().WithModel(...), or make it "
                + "configurable and pass it in the settings.");

        var provider = _services.GetService<IChatClientProvider>()
            ?? throw new InvalidOperationException(
                "No IChatClientProvider is registered: call WithChatClientProvider<T>() after AddTypeshift().");

        var client = await provider.GetChatClientAsync(model, ct);
        var run = options is ResolvedChatOptions resolved ? resolved.RunOptions : Agent.RunOptions;

        // D29: outermost the conversation, innermost the provider's client behind a wrapper that never disposes it,
        // with approval above the tool loop and the checkpoints inside it
        var loop = new FunctionInvokingChatClient(
            new CheckpointChatClient(new UndisposedChatClient(client)),
            _services.GetService<ILoggerFactory>(),
            _services)
        {
            MaximumIterationsPerRequest = run.MaxToolIterations,
            MaximumConsecutiveErrorsPerRequest = run.MaxConsecutiveToolErrors,
            AllowConcurrentInvocation = run.ConcurrentToolCalls,

            // An approved plan runs whatever mode the run continues in
            AdditionalTools = [ExitPlanModeFunction.Instance]
        };

        var approval = new ApprovalChatClient(new AuthorNameChatClient(loop, Agent.Name), loop);
        return new ConversationChatClient(approval, Agent, _services);
    }

    // D27: the agent's code, its stored settings, then the run's; values a layer leaves null keep the one below
    internal async Task<ChatOptions> ResolveOptionsCoreAsync(object? settingsId, object? settings, CancellationToken ct)
    {
        var declaration = Agent.GeneratedDeclaration;
        var stored = settingsId is null ? null : await declaration.LoadSettingsAsync(Store, settingsId, ct);
        IReadOnlyList<object> layers = [.. new[] { stored, settings }.OfType<object>()];

        var options = new ResolvedChatOptions(Agent.ChatOptions) { Instructions = Agent.Instructions };
        var run = Agent.RunOptions;

        foreach (var layer in layers)
        {
            run = declaration.ApplySettings(options, run, layer);
        }

        var tools = declaration.CreateTools(Agent, layers, _services.GetRequiredService<ToolJson>());

        options.Tools = [.. tools, .. options.Tools ?? []];
        options.RunOptions = run;
        options.RunSetsMode = settings is not null && SetsMode(declaration, settings);
        options.SettingsId = settingsId;

        return options;
    }

    // A run without input resumes the conversation's interrupted run (D50)
    internal async Task<ChatResponse> RunCoreAsync(
        ChatMessage? input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversationId, settings, ct);
        using var client = await CreateChatClientAsync(options, ct);

        return await client.GetResponseAsync(input is null ? [] : [input], options, ct);
    }

    internal async Task<ChatResponse<TResult>> RunCoreAsync<TResult>(
        ChatMessage input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversationId, settings, ct);
        using var client = await CreateChatClientAsync(options, ct);
        var json = _services.GetRequiredService<ToolJson>();

        return await client.GetResponseAsync<TResult>([input], json.SerializerOptions, options, cancellationToken: ct);
    }

    internal async IAsyncEnumerable<ChatResponseUpdate> RunStreamingCoreAsync(
        ChatMessage? input,
        object? settingsId,
        object? conversationId,
        object? settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var options = await ResolveRunAsync(settingsId, conversationId, settings, ct);
        using var client = await CreateChatClientAsync(options, ct);

        await foreach (var update in client.GetStreamingResponseAsync(input is null ? [] : [input], options, ct))
        {
            yield return update;
        }
    }

    internal IStoreAccess Store => _services.GetRequiredService<IStoreAccess>();

    // A layer sets the mode when it moves two different modes to the same one
    private static bool SetsMode(AgentDeclaration declaration, object settings) =>
        declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Default }, settings).Mode
        == declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Plan }, settings).Mode;

    private async Task<ResolvedChatOptions> ResolveRunAsync(
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
    {
        var options = (ResolvedChatOptions)await ResolveOptionsCoreAsync(settingsId, settings, ct);
        options.Conversation = conversationId;
        return options;
    }
}
