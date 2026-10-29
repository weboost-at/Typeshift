using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

    // D56: boxed, of the build's provider id type
    internal object? Provider { get; set; }

    internal RunOptions RunOptions { get; set; } = new();

    internal List<Func<IServiceProvider, IContextProvider>> ContextProviders { get; } = [];

    internal List<AgentUse> SubAgents { get; } = [];

    internal List<Func<IServiceProvider, IAgentHost>> Handoffs { get; } = [];

    internal List<McpServerUse> McpServers { get; } = [];

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
    /// Names the kind a store keys the agent's <c>Settings</c> by, in place of its class name, so renaming the class
    /// keeps its stored settings.
    /// </summary>
    /// <param name="kind">A constant, unique among the agents the host sees.</param>
    protected void WithStorageKind(string kind)
    {
    }

    /// <summary>
    /// Raises the schema version of the agent's stored <c>Settings</c>, 1 by default, after renaming or removing a
    /// configurable member; stores see it and migrate older documents.
    /// </summary>
    /// <param name="version">A constant above the last one.</param>
    protected void WithSchemaVersion(int version)
    {
    }

    /// <summary>
    /// Sets the chat options every run starts from and marks those a run or stored settings may change.
    /// </summary>
    /// <returns>The chain of chat options.</returns>
    protected ChatOptionsBuilder WithChatOptions() => new(this);

    // D4: the generator reads the chain at compile time; at runtime it declares nothing
    /// <summary>
    /// Declares a tool of this agent's own, backed by a static method of the agent, and offers it after the tools of
    /// the toolsets. It takes the same chain as a toolset's tool: its name, description, settings and approval.
    /// </summary>
    /// <param name="method">The method group: <c>AddTool(LookUpOrder)</c>.</param>
    /// <returns>A chain that renames, describes, marks and configures the tool.</returns>
    protected ToolBuilder AddTool(Delegate method) => ToolBuilder.Instance;

    /// <summary>
    /// Offers the tools of a toolset: all of them, or those <c>Include</c> picks, or all but those <c>Exclude</c>
    /// picks.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <returns>The use, to include or exclude tools.</returns>
    protected ToolsetUse<TToolset> UseToolset<TToolset>() where TToolset : Toolset, new() =>
        ToolsetUse<TToolset>.Instance;

    /// <summary>
    /// Offers another agent as a tool named like it, described by its <c>WithDescription</c>, with one <c>input</c>
    /// parameter. A call runs it in a fresh context, with the caller's settings id, mode, standing approvals and
    /// rules, and returns its answer's text.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <returns>The use, to run it in the background.</returns>
    protected AgentUse UseAgent<TAgent>() where TAgent : Agent
    {
        var use = new AgentUse(services => services.GetRequiredService<AgentHost<TAgent>>());
        SubAgents.Add(use);
        return use;
    }

    /// <summary>
    /// Lets the model hand the conversation to another agent through a tool <c>handoff_to_{name}</c>. The other agent
    /// answers in the same run and goes on answering the conversation's later runs.
    /// </summary>
    /// <typeparam name="TAgent">The agent to hand off to.</typeparam>
    protected void UseHandoff<TAgent>() where TAgent : Agent =>
        Handoffs.Add(services => services.GetRequiredService<AgentHost<TAgent>>());

    /// <summary>
    /// Offers the tools of an MCP server, which the host's <see cref="IMcpToolProvider"/> connects by name.
    /// </summary>
    /// <param name="server">The server's name, as the host knows it.</param>
    /// <returns>The use, to require approval or mark its tools as reading only.</returns>
    protected McpServerUse UseMcpServer(string server)
    {
        var use = new McpServerUse(server);
        McpServers.Add(use);
        return use;
    }

    /// <summary>
    /// Adds instructions, messages or tools to every run from a provider the run's services resolve, or create when
    /// they have none registered.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    protected void UseContext<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IContextProvider =>
        ContextProviders.Add(services => ActivatorUtilities.GetServiceOrCreateInstance<TProvider>(services));

    /// <summary>
    /// Sets how a run goes: its mode, its tool loop's limits, and when its history is compacted. Long-running agents
    /// raise the limits and turn compaction on.
    /// </summary>
    /// <returns>The chain of run options.</returns>
    protected RunOptionsBuilder WithRunOptions() => new(this);

    /// <summary>
    /// The agent's instructions, its system prompt; called whenever a run's options are resolved.
    /// </summary>
    /// <returns>The instructions, or null for none.</returns>
    protected virtual string? GetInstructions() => null;
}
