using System.ComponentModel;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// The base of a partial class that declares an agent in its constructor: chat options, tools and their settings. The
/// generator adds the agent's <c>Settings</c> record, a <c>Configure{Tool}()</c> per offered tool with settings, and
/// its typed members on <see cref="AgentHost{TAgent}"/>.
/// </summary>
public abstract class Agent
{
    /// <summary>
    /// The agent's name: the one <see cref="WithName"/> gives, or the class name in snake_case. Messages it writes
    /// carry it as their author.
    /// </summary>
    public string Name => GeneratedDeclaration.Name;

    /// <summary>
    /// What the agent does, if <see cref="WithDescription"/> says so.
    /// </summary>
    public string? Description => GeneratedDeclaration.Description;

    // D4: the values the chains record; what they declare is in the generated declaration
    internal ChatOptions ChatOptions { get; } = new();

    internal RunOptions RunOptions { get; set; } = new();

    // Protected members stay protected, never protected internal, which an assembly that sees the library's internals
    // would have to override as protected internal
    internal AgentDeclaration GeneratedDeclaration => Declaration;

    internal string? Instructions => GetInstructions();

    /// <summary>
    /// What the generator read from the constructor.
    /// </summary>
    /// <exception cref="InvalidOperationException">The agent was not compiled with the generator.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected virtual AgentDeclaration Declaration =>
        throw new InvalidOperationException(
            $"{GetType().Name} was not compiled with the Typeshift generator: make it partial and reference the "
            + "Typeshift package in its project.");

    // D4: the generator reads the constant; at runtime the call declares nothing
    /// <summary>
    /// Names the agent.
    /// </summary>
    /// <param name="name">A constant.</param>
    protected void WithName(string name)
    {
    }

    /// <summary>
    /// Describes what the agent does.
    /// </summary>
    /// <param name="description">A constant.</param>
    protected void WithDescription(string description)
    {
    }

    /// <summary>
    /// Sets the chat options every run starts from and marks those a run or stored settings may change.
    /// </summary>
    /// <returns>The chain of chat options.</returns>
    protected ChatOptionsBuilder WithChatOptions() => new(ChatOptions);

    /// <summary>
    /// Offers the tools of a toolset: all of them, or those <c>Include</c> picks, or all but those <c>Exclude</c>
    /// picks.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <returns>The use, to include or exclude tools.</returns>
    protected ToolsetUse<TToolset> UseToolset<TToolset>() where TToolset : Toolset, new() =>
        ToolsetUse<TToolset>.Instance;

    /// <summary>
    /// Sets how a run goes: its tool loop's limits.
    /// </summary>
    /// <returns>The chain of run options.</returns>
    protected RunOptionsBuilder WithRunOptions() => new(this);

    /// <summary>
    /// The agent's instructions, its system prompt; called whenever a run's options are resolved.
    /// </summary>
    /// <returns>The instructions, or null for none.</returns>
    protected virtual string? GetInstructions() => null;
}
