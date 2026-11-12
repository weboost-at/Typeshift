using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// What the generator read from an agent's constructor: its name, its tools and its configurable options.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AgentDeclaration
{
    private protected AgentDeclaration()
    {
    }

    /// <summary>
    /// The name <c>WithName</c> gives, or the class name in snake_case.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The description <c>WithDescription</c> or the agent's summary gives.
    /// </summary>
    public abstract string? Description { get; }

    internal abstract RunOptions ApplySettings(ChatOptions options, RunOptions run, object settings);

    internal abstract IEnumerable<AITool> CreateTools(Agent agent, IReadOnlyList<object> layers, ToolJson json);

    internal abstract Task<object?> LoadSettingsAsync(StoreAccess store, StoreId id, CancellationToken ct);

    // D66: the agent's own settings, under no id
    internal abstract Task<object?> LoadAgentSettingsAsync(StoreAccess store, CancellationToken ct);
}

/// <summary>
/// The declaration of an agent and its <c>Settings</c> record.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
/// <typeparam name="TSettings">The agent's settings record.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AgentDeclaration<TAgent, TSettings> : AgentDeclaration
    where TAgent : Agent
    where TSettings : class, IStorable<TSettings>
{
    internal sealed override RunOptions ApplySettings(ChatOptions options, RunOptions run, object settings) =>
        ApplySettings(options, run, (TSettings)settings);

    internal sealed override IEnumerable<AITool> CreateTools(
        Agent agent,
        IReadOnlyList<object> layers,
        ToolJson json) =>
        CreateTools((TAgent)agent, [.. layers.Cast<TSettings>()], json);

    internal sealed override async Task<object?> LoadSettingsAsync(
        StoreAccess store,
        StoreId id,
        CancellationToken ct) =>
        (await store.LoadAsync<TSettings>(id, ct))?.Data;

    internal sealed override async Task<object?> LoadAgentSettingsAsync(StoreAccess store, CancellationToken ct) =>
        (await store.LoadAgentSettingsAsync<TSettings>(ct))?.Data;

    /// <summary>
    /// Applies the chat and run options one settings layer sets.
    /// </summary>
    /// <param name="options">The chat options, changed in place.</param>
    /// <param name="run">The run options of the layers below.</param>
    /// <param name="settings">The layer.</param>
    /// <returns>The run options with what the layer sets.</returns>
    protected abstract RunOptions ApplySettings(ChatOptions options, RunOptions run, TSettings settings);

    // D77: the generated code knows the provider's type, so the run asks the IChatClientProvider of that type
    /// <summary>
    /// Sets the provider of the run's model, as a settings layer names it.
    /// </summary>
    /// <typeparam name="TProviderId">The agent's provider type.</typeparam>
    /// <param name="options">The chat options being resolved.</param>
    /// <param name="provider">The provider.</param>
    protected static void SetProvider<TProviderId>(ChatOptions options, TProviderId provider)
        where TProviderId : notnull
    {
        if (options is ResolvedChatOptions resolved) resolved.Provider = new ProviderId<TProviderId>(provider);
    }

    /// <summary>
    /// Sets the provider of the summary model, as a settings layer names it.
    /// </summary>
    /// <typeparam name="TProviderId">The agent's provider type.</typeparam>
    /// <param name="options">The chat options being resolved.</param>
    /// <param name="provider">The provider.</param>
    protected static void SetSummaryProvider<TProviderId>(ChatOptions options, TProviderId provider)
        where TProviderId : notnull
    {
        if (options is ResolvedChatOptions resolved) resolved.SummaryProvider = new ProviderId<TProviderId>(provider);
    }

    /// <summary>
    /// Creates the offered tools with their settings: the agent's <c>Configure{Tool}()</c>, then each layer.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="layers">The settings layers, lowest first.</param>
    /// <param name="json">How the tools bind their arguments.</param>
    /// <returns>The tools, in the order the agent offers them.</returns>
    protected abstract IEnumerable<AITool> CreateTools(TAgent agent, IReadOnlyList<TSettings> layers, ToolJson json);
}
