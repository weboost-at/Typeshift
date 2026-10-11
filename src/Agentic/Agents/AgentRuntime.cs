using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// Runs an agent: loads and stores its configuration, resolves the options of a run and creates its chat client.
/// </summary>
public class AgentRuntime
{
    private readonly AgentRuntimeServices _services;

    internal AgentRuntime(AgentDefinition definition, AgentRuntimeServices services)
    {
        Definition = definition;
        _services = services;
    }

    /// <summary>
    /// The agent as its code or its stored record defines it.
    /// </summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// The stored configuration, empty while nothing is stored or no store is registered.
    /// </summary>
    public async Task<AgentConfiguration> LoadConfigurationAsync(CancellationToken ct = default) =>
        _services.Configurations is { } store
            ? await store.LoadAsync(Definition.UniqueName, ct) ?? new AgentConfiguration(Definition.UniqueName)
            : new AgentConfiguration(Definition.UniqueName);

    /// <summary>
    /// The stored configuration through the agent's generated, allowlisted view.
    /// </summary>
    public async Task<TView> LoadConfigurationAsync<TView>(CancellationToken ct = default)
        where TView : AgentConfigurationView, IAgentConfigurationView<TView> =>
        TView.Create(await LoadConfigurationAsync(ct), _services.Json.SerializerOptions);

    /// <summary>
    /// Changes the stored configuration and stores it.
    /// </summary>
    public async Task UpdateConfigurationAsync(
        Func<AgentConfiguration, AgentConfiguration> update,
        CancellationToken ct = default)
    {
        if (_services.Configurations is not { } store)
            throw new InvalidOperationException(
                "No IAgentConfigurationStore is registered: register one to store the configuration of "
                + Definition.UniqueName + ".");

        var updated = update(await LoadConfigurationAsync(ct)) with
        {
            UpdatedAt = _services.Time.GetUtcNow()
        };

        await store.SaveAsync(updated, ct);
    }

    /// <summary>
    /// Changes the stored configuration through the agent's generated, allowlisted view and stores it.
    /// </summary>
    public Task UpdateConfigurationAsync<TView>(Action<TView> configure, CancellationToken ct = default)
        where TView : AgentConfigurationView, IAgentConfigurationView<TView> =>
        UpdateConfigurationAsync(configuration => Configure(configuration, configure), ct);

    /// <summary>
    /// The chat options of a run: the definition, then the stored overrides, then the run's own.
    /// </summary>
    public async Task<ChatOptions> ResolveChatOptionsAsync(RunOverrides? run = null, CancellationToken ct = default)
    {
        var configuration = await LoadConfigurationAsync(ct);
        var toolDefaults = await _services.ToolDefaults.LoadAsync([.. Definition.Tools.Select(tool => tool.Name)], ct);

        return Definition.CreateChatOptions(configuration, toolDefaults, run ?? RunOverrides.None, _services.Json);
    }

    /// <summary>
    /// A chat client for the configured model, invoking the agent's tools.
    /// </summary>
    public async Task<IChatClient> CreateChatClientAsync(CancellationToken ct = default)
    {
        var configuration = await LoadConfigurationAsync(ct);

        if (configuration.Model is not { } model)
            throw new ConfigurationRequiredException($"No model is configured for {Definition.UniqueName}.");

        if (_services.ChatClients is not { } provider)
            throw new InvalidOperationException(
                "No IChatClientProvider is registered: register one to create chat clients.");

        var client = await provider.GetChatClientAsync(model, ct);

        return client
            .AsBuilder()
            .UseFunctionInvocation()
            .Build(_services.Services);
    }

    private AgentConfiguration Configure<TView>(AgentConfiguration configuration, Action<TView> configure)
        where TView : AgentConfigurationView, IAgentConfigurationView<TView>
    {
        var view = TView.Create(configuration, _services.Json.SerializerOptions);
        configure(view);
        return view.Stored;
    }
}

/// <summary>
/// The runtime of the code agent <typeparamref name="TAgent"/>, registered per agent.
/// </summary>
public sealed class AgentRuntime<TAgent> : AgentRuntime where TAgent : AgentDefinition
{
    internal AgentRuntime(TAgent definition, AgentRuntimeServices services) : base(definition, services)
    {
    }
}
