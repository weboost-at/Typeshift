using System.Collections.Frozen;
using System.ComponentModel;
using Typeshift.Conversations;
using Typeshift.Json;
using Typeshift.Persistence;
using Typeshift.Settings;
using Typeshift.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Agents;

/// <summary>
/// Runs an agent. The generator adds the typed members of each agent as extension members of
/// <see cref="AgentRuntime{TAgent}"/>: <c>LoadSettingsAsync</c>, <c>SaveSettingsAsync</c>, <c>ConfigureAsync</c>
/// and <c>ResolveChatOptionsAsync</c>.
/// </summary>
public class AgentRuntime
{
    private readonly IServiceProvider _services;
    private readonly AgentJson _json;

    internal AgentRuntime(AgentDefinition definition, IServiceProvider services)
    {
        Definition = definition;
        _services = services;
        _json = services.GetRequiredService<AgentJson>();
    }

    /// <summary>
    /// The agent as its code defines it.
    /// </summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// Creates a chat client that runs the agent: it tracks its conversations and invokes the agent's tools.
    /// </summary>
    /// <param name="options">
    /// The resolved options of the run; their <see cref="ChatOptions.ModelId"/> picks the model.
    /// </param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client, to call with the same <paramref name="options"/>.</returns>
    /// <exception cref="ConfigurationRequiredException">The options name no model.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IChatClientProvider"/> is registered.</exception>
    public async Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)
    {
        if (options.ModelId is not { } model)
            throw new ConfigurationRequiredException(
                $"No model is configured for {Definition.Name}: set the model in its settings or with "
                + "WithChatOptions().WithModel(...).");

        if (_services.GetService<IChatClientProvider>() is not { } provider)
            throw new InvalidOperationException(
                "No IChatClientProvider is registered: register one to create chat clients.");

        var client = await provider.GetChatClientAsync(model, ct);

        return client
            .AsBuilder()
            .UseConversationTracking(model)
            .UseFunctionInvocation()
            .Build(_services);
    }

    // D35: the Core members are public for the generated typed members in other assemblies, hidden from editors
    /// <summary>
    /// Loads the agent's settings; the generated <c>LoadSettingsAsync</c> calls it.
    /// </summary>
    /// <typeparam name="TSettings">The agent's generated settings.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id the settings were saved under.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The stored settings, or empty settings when none are stored.</returns>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<TSettings> LoadSettingsCoreAsync<TSettings, TId>(TId id, CancellationToken ct)
        where TSettings : class, IAgentSettings, IStorable<TSettings>, new()
        where TId : notnull =>
        await Stores.Get<TId>(_services).LoadAsync<TSettings>(id, ct) ?? new TSettings();

    /// <summary>
    /// Saves the agent's settings; the generated <c>SaveSettingsAsync</c> calls it.
    /// </summary>
    /// <typeparam name="TSettings">The agent's generated settings.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id to save the settings under.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task SaveSettingsCoreAsync<TSettings, TId>(TId id, TSettings settings, CancellationToken ct)
        where TSettings : class, IAgentSettings, IStorable<TSettings>
        where TId : notnull =>
        Stores.Get<TId>(_services).SaveAsync(id, settings, ct);

    /// <summary>
    /// Changes the agent's stored settings and saves them; the generated <c>ConfigureAsync</c> calls it.
    /// </summary>
    /// <typeparam name="TSettings">The agent's generated settings.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id the settings are stored under.</param>
    /// <param name="configure">Returns the changed settings.</param>
    /// <param name="ct">Cancels the load and the save.</param>
    /// <returns>The saved settings.</returns>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<TSettings> ConfigureSettingsCoreAsync<TSettings, TId>(
        TId id,
        Func<TSettings, TSettings> configure,
        CancellationToken ct)
        where TSettings : class, IAgentSettings, IStorable<TSettings>, new()
        where TId : notnull
    {
        var configured = configure(await LoadSettingsCoreAsync<TSettings, TId>(id, ct));
        await SaveSettingsCoreAsync(id, configured, ct);
        return configured;
    }

    /// <summary>
    /// Resolves the options of a run from the code alone; the generated <c>ResolveChatOptionsAsync</c> calls it.
    /// </summary>
    /// <param name="run">Settings for this run only, or null.</param>
    /// <param name="conversationId">The conversation the run continues, or null.</param>
    /// <returns>The options, with the agent's tools.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ChatOptions ResolveChatOptionsCore(IAgentSettings? run, string? conversationId) =>
        Definition.CreateChatOptions(
            stored: null,
            FrozenDictionary<ToolName, IToolOverrides>.Empty,
            run,
            conversationId,
            _json);

    /// <summary>
    /// Resolves the options of a run from the code and the settings stored under an id; the generated
    /// <c>ResolveChatOptionsAsync</c> calls it.
    /// </summary>
    /// <typeparam name="TSettings">The agent's generated settings.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id the agent's settings and its tools' defaults are stored under.</param>
    /// <param name="run">Settings for this run only, or null.</param>
    /// <param name="conversationId">The conversation the run continues, or null.</param>
    /// <param name="ct">Cancels the loads.</param>
    /// <returns>The options, with the agent's tools.</returns>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<ChatOptions> ResolveChatOptionsCoreAsync<TSettings, TId>(
        TId id,
        IAgentSettings? run,
        string? conversationId,
        CancellationToken ct)
        where TSettings : class, IAgentSettings, IStorable<TSettings>
        where TId : notnull
    {
        var store = Stores.Get<TId>(_services);
        var stored = await store.LoadAsync<TSettings>(id, ct);
        var toolDefaults = await LoadToolDefaultsAsync(store, id, ct);

        return Definition.CreateChatOptions(stored, toolDefaults, run, conversationId, _json);
    }

    private async Task<IReadOnlyDictionary<ToolName, IToolOverrides>> LoadToolDefaultsAsync<TId>(
        ITypeshiftStore<TId> store,
        TId id,
        CancellationToken ct)
        where TId : notnull
    {
        var defaults = await Task.WhenAll(Definition.Tools.Select(tool =>
            tool.Toolset.LoadDefaultsAsync(tool, store, id, ct)));

        return defaults.OfType<IToolOverrides>().ToDictionary(overrides => overrides.Tool);
    }
}

/// <summary>
/// The runtime of the agent <typeparamref name="TAgent"/>, registered per agent; its typed members are generated.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
public sealed class AgentRuntime<TAgent> : AgentRuntime where TAgent : AgentDefinition
{
    internal AgentRuntime(TAgent definition, IServiceProvider services) : base(definition, services)
    {
    }
}
