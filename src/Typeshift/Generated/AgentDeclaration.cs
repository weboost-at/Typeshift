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
    /// The description <c>WithDescription</c> gives.
    /// </summary>
    public abstract string? Description { get; }

    internal abstract RunOptions ApplySettings(ChatOptions options, RunOptions run, object settings);

    internal abstract IEnumerable<AITool> CreateTools(Agent agent, IReadOnlyList<object> layers, ToolJson json);
}

/// <summary>
/// The declaration of an agent and its <c>Settings</c> record.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
/// <typeparam name="TSettings">The agent's settings record.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AgentDeclaration<TAgent, TSettings> : AgentDeclaration
    where TAgent : Agent
    where TSettings : class
{
    internal sealed override RunOptions ApplySettings(ChatOptions options, RunOptions run, object settings) =>
        ApplySettings(options, run, (TSettings)settings);

    internal sealed override IEnumerable<AITool> CreateTools(
        Agent agent,
        IReadOnlyList<object> layers,
        ToolJson json) =>
        CreateTools((TAgent)agent, [.. layers.Cast<TSettings>()], json);

    /// <summary>
    /// Applies the chat and run options one settings layer sets.
    /// </summary>
    /// <param name="options">The chat options, changed in place.</param>
    /// <param name="run">The run options of the layers below.</param>
    /// <param name="settings">The layer.</param>
    /// <returns>The run options with what the layer sets.</returns>
    protected abstract RunOptions ApplySettings(ChatOptions options, RunOptions run, TSettings settings);

    /// <summary>
    /// Creates the offered tools with their settings: the agent's <c>Configure{Tool}()</c>, then each layer.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="layers">The settings layers, lowest first.</param>
    /// <param name="json">How the tools bind their arguments.</param>
    /// <returns>The tools, in the order the agent offers them.</returns>
    protected abstract IEnumerable<AITool> CreateTools(TAgent agent, IReadOnlyList<TSettings> layers, ToolJson json);
}
