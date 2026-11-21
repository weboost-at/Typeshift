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
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/defining-agents">Agents</see>.
/// </summary>
public abstract class Agent
{
    /// <summary>
    /// The agent's name: the one <see cref="WithName"/> gives, or the class name in snake_case. Messages it writes
    /// carry it as their author.
    /// </summary>
    public string Name => GeneratedDeclaration.Name;

    /// <summary>
    /// What the agent does: the text of <see cref="WithDescription"/>, or else the summary of the agent's XML docs.
    /// </summary>
    public string? Description => GeneratedDeclaration.Description;

    // D4: the values the chains record; what they declare is in the generated declaration
    internal ChatOptions ChatOptions { get; } = new();

    // D77: WithModel's provider, with its type
    internal ProviderId? Provider { get; set; }

    internal RunOptions RunOptions { get; set; } = new();

    internal List<Func<IServiceProvider, ContextSource>> ContextProviders { get; } = [];

    internal List<AgentUse> SubAgents { get; } = [];

    internal List<Func<IServiceProvider, IAgentHost>> Handoffs { get; } = [];

    internal List<McpServerUse> McpServers { get; } = [];

    // D52: the tools of WithReadOnlyTools, which plan mode offers, by reference
    internal HashSet<AITool> ReadOnlyTools { get; } = new(ReferenceEqualityComparer.Instance);

    // D82: whether the model may ask the user through ask_user
    internal bool AsksUser { get; private set; }

    // Protected members stay protected, never protected internal, which an assembly that sees the library's internals
    // would have to override as protected internal
    internal AgentDeclaration GeneratedDeclaration => Declaration;

    // D21: an agent is one instance for every run, so what a run's instructions depend on comes from its services
    internal string? InstructionsFor(IServiceProvider services) => GetInstructions(services);

    // D70: what WithInstructions set, which GetInstructions returns unless the agent overrides it
    private string? _instructions;

    /// <summary>
    /// What the generator read from the constructor.
    /// </summary>
    /// <exception cref="InvalidOperationException">The agent was not compiled with the generator.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected virtual AgentDeclaration Declaration =>
        throw new InvalidOperationException(
            $"{GetType().Name} was not compiled with the Typeshift generator: make it partial, name Agent itself in "
            + "its base list, not through a using alias, and reference the Typeshift package in its project.");

    // D4: the generator reads the constant; at runtime the call declares nothing
    /// <summary>
    /// Names the agent.
    /// </summary>
    /// <param name="name">A constant.</param>
    protected void WithName(string name)
    {
    }

    /// <summary>
    /// Sets the agent's instructions, its system prompt, when they are the same for every run. Override
    /// <see cref="GetInstructions"/> for instructions your code builds from the run's services when a run starts.
    /// </summary>
    /// <param name="instructions">The instructions.</param>
    protected void WithInstructions(string instructions) => _instructions = instructions;

    /// <summary>
    /// Describes what the agent does, in place of the summary of its XML docs.
    /// </summary>
    /// <param name="description">A constant.</param>
    protected void WithDescription(string description)
    {
    }

    /// <summary>
    /// Names the kind a store keys the agent's <c>Settings</c> by, in place of its class name, so renaming the class
    /// keeps its stored settings.
    /// </summary>
    /// <param name="kind">A constant, unique among the agents your app sees.</param>
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
    protected ToolsetUse<TToolset> UseToolset<TToolset>() where TToolset : Toolset =>
        ToolsetUse<TToolset>.Instance;

    /// <summary>
    /// Offers another agent as a tool named like it and described like it, with one <c>input</c>
    /// parameter. A call runs it in a fresh context, with the caller's settings id, mode, standing approvals and
    /// rules, and returns its answer's text, or its JSON with <c>WithResult</c>.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <returns>The use, to run it in the background or to hold its answer to a type.</returns>
    protected AgentUse UseAgent<TAgent>() where TAgent : Agent
    {
        var use = new AgentUse(AgentHost<TAgent>.In);
        SubAgents.Add(use);
        return use;
    }

    /// <summary>
    /// Lets the model hand the conversation to another agent through a tool <c>handoff_to_{name}</c>. The other agent
    /// answers in the same run and goes on answering the conversation's later runs.
    /// </summary>
    /// <typeparam name="TAgent">The agent to hand off to.</typeparam>
    protected void UseHandoff<TAgent>() where TAgent : Agent =>
        Handoffs.Add(AgentHost<TAgent>.In);

    /// <summary>
    /// Offers the tools of an MCP server, which your app's <see cref="IMcpToolProvider"/> connects by name.
    /// </summary>
    /// <param name="server">The server's name, as your app knows it.</param>
    /// <returns>The use, to require approval or mark its tools as reading only.</returns>
    protected McpServerUse UseMcpServer(string server)
    {
        var use = new McpServerUse(server);
        McpServers.Add(use);
        return use;
    }

    /// <summary>
    /// Offers the model the built-in tool <c>ask_user</c>, which asks the user one to four questions, each with two
    /// to four options. A call of it ends the run with a <see cref="QuestionDecision"/>, and your app's answers are
    /// the call's result. It needs a conversation to wait in, and a sub-agent is not offered it.
    /// </summary>
    protected void UseQuestions() => AsksUser = true;

    /// <summary>
    /// Adds instructions, messages or tools to every run from a provider the run's services resolve, or create when
    /// they have none registered. A provider they create is created for each pass of the run and disposed once the
    /// pass ended; register a provider that keeps state or resources from one pass to the next, as a scoped service.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    protected void UseContext<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IContextProvider =>
        ContextProviders.Add(ContextChatClient.Of<TProvider>);

    /// <summary>
    /// Sets how a run goes: its mode, its tool loop's limits, and when its history is compacted. Long-running agents
    /// raise the limits and turn compaction on.
    /// </summary>
    /// <returns>The chain of run options.</returns>
    protected RunOptionsBuilder WithRunOptions() => new(this);

    /// <summary>
    /// The agent's instructions, its system prompt; called whenever a run's options are resolved. Without an
    /// override, the instructions <see cref="WithInstructions"/> set. One instance of the agent serves every run, so
    /// an override builds what depends on the run from <paramref name="services"/>, such as the customer it serves.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/defining-agents#instructions-built-per-run">Instructions
    /// built per run</see>.
    /// </summary>
    /// <param name="services">
    /// The run's services: those of the scope of the host that runs the agent, which its tools resolve from too.
    /// </param>
    /// <returns>The instructions, or null for none.</returns>
    protected virtual string? GetInstructions(IServiceProvider services) => _instructions;
}
