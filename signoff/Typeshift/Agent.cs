using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// The base of a partial class that declares an agent in its constructor: chat options, tools and their settings,
/// agents it calls or hands off to, MCP servers, context and compaction. The generator adds the agent's
/// <c>Settings</c> record, a <c>Configure{Tool}()</c> per offered tool with settings, and its typed members on
/// <see cref="AgentHost{TAgent}"/>.
/// </summary>
public abstract class Agent
{
    /// <summary>
    /// The agent's name: the one <see cref="WithName"/> gives, or the class name in snake_case. Messages it writes
    /// carry it as their author; as a tool or a handoff target it names the tool.
    /// </summary>
    public string Name => throw null!;

    /// <summary>
    /// What the agent does, if <see cref="WithDescription"/> says so; required to use it as a tool or hand off to it.
    /// </summary>
    public string? Description => throw null!;

    /// <summary>
    /// Names the agent.
    /// </summary>
    /// <param name="name">A constant; lower_snake_case when the agent is a tool or a handoff target.</param>
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
    protected void WithSettingsVersion(int version)
    {
    }

    /// <summary>
    /// Sets the chat options every run starts from and marks those a run or stored settings may change.
    /// </summary>
    /// <returns>The chain of chat options.</returns>
    protected ChatOptionsBuilder WithChatOptions() => new();

    /// <summary>
    /// Offers the tools of a toolset: all of them, or those <c>Include</c> picks, or all but those <c>Exclude</c>
    /// picks.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <returns>The use, to include or exclude tools.</returns>
    protected ToolsetUse<TToolset> UseToolset<TToolset>() where TToolset : Toolset, new() => new();

    /// <summary>
    /// Offers another agent as a sub-agent: a tool named and described like the agent. A call runs it in a fresh
    /// context, with the same settings id, mode, standing approvals and rules, and returns its answer's text.
    /// </summary>
    /// <typeparam name="TAgent">The agent to call.</typeparam>
    /// <returns>The use, to run the sub-agent in the background.</returns>
    protected AgentUse UseAgent<TAgent>() where TAgent : Agent => new();

    /// <summary>
    /// Lets the model hand the conversation to another agent through a tool <c>handoff_to_{name}</c>. The other agent
    /// answers in the same run and goes on answering the conversation's later runs.
    /// </summary>
    /// <typeparam name="TAgent">The agent to hand off to.</typeparam>
    protected void UseHandoff<TAgent>() where TAgent : Agent
    {
    }

    /// <summary>
    /// Offers the tools of an MCP server, which the host's <see cref="IMcpToolProvider"/> connects by name.
    /// </summary>
    /// <param name="server">The server's name, as the host knows it.</param>
    /// <returns>The use, to require approval.</returns>
    protected McpServerUse UseMcpServer(string server) => new();

    /// <summary>
    /// Adds instructions, messages or tools to every run from a provider the run's services resolve.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    protected void UseContext<TProvider>() where TProvider : class, IContextProvider
    {
    }

    /// <summary>
    /// Sets how a run goes: its tool loop's limits and when its history is compacted. Long-running agents raise the
    /// limits and turn compaction on.
    /// </summary>
    /// <returns>The chain of run options.</returns>
    protected RunOptionsBuilder WithRunOptions() => new();

    /// <summary>
    /// The agent's instructions, its system prompt; called whenever a run's options are resolved.
    /// </summary>
    /// <returns>The instructions, or null for none.</returns>
    protected virtual string? GetInstructions() => null;
}

/// <summary>
/// The chat options an agent starts every run from, and those that may change at runtime.
/// </summary>
public sealed class ChatOptionsBuilder
{
    internal ChatOptionsBuilder()
    {
    }

    /// <summary>
    /// Sets the model, as the host's <see cref="IChatClientProvider"/> names it. <c>Configurable(o => o.ModelId)</c>
    /// lets stored settings or a run choose another.
    /// </summary>
    public ChatOptionsBuilder WithModel(string model) => this;

    public ChatOptionsBuilder WithTemperature(float temperature) => this;

    public ChatOptionsBuilder WithMaxOutputTokens(int tokens) => this;

    public ChatOptionsBuilder WithTopP(float topP) => this;

    public ChatOptionsBuilder WithTopK(int topK) => this;

    public ChatOptionsBuilder WithFrequencyPenalty(float penalty) => this;

    public ChatOptionsBuilder WithPresencePenalty(float penalty) => this;

    public ChatOptionsBuilder WithSeed(long seed) => this;

    public ChatOptionsBuilder WithReasoning(ReasoningOptions reasoning) => this;

    public ChatOptionsBuilder WithResponseFormat(ChatResponseFormat format) => this;

    public ChatOptionsBuilder WithStopSequences(params string[] sequences) => this;

    public ChatOptionsBuilder WithToolMode(ChatToolMode mode) => this;

    public ChatOptionsBuilder WithAllowMultipleToolCalls(bool allow) => this;

    /// <summary>
    /// Adds tools of no toolset, such as a <see cref="HostedWebSearchTool"/> or a <see cref="HostedMcpServerTool"/>.
    /// </summary>
    public ChatOptionsBuilder WithTools(params AITool[] tools) => this;

    /// <summary>
    /// Sets a provider-specific option.
    /// </summary>
    public ChatOptionsBuilder WithAdditionalProperty<TValue>(string key, TValue value) => this;

    /// <summary>
    /// Lets an option change at runtime, whether the code sets it or not. The lambda never runs.
    /// </summary>
    /// <typeparam name="TOption">The option's type.</typeparam>
    /// <param name="option">The option, <c>o => o.Temperature</c>.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder Configurable<TOption>(Func<ChatOptions, TOption> option) => this;
}

/// <summary>
/// An agent's use of a toolset. One use includes or excludes, never both.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolsetUse<TToolset> where TToolset : Toolset
{
    internal ToolsetUse()
    {
    }

    /// <summary>
    /// Offers only the tools picked, in the order picked.
    /// </summary>
    public ToolInclusion<TToolset> Include(Func<ToolList<TToolset>, ToolName> tool) => new();

    /// <summary>
    /// Offers every tool but those picked.
    /// </summary>
    public ToolExclusion<TToolset> Exclude(Func<ToolList<TToolset>, ToolName> tool) => new();
}

/// <summary>
/// A use that offers only the tools it includes.
/// </summary>
public sealed class ToolInclusion<TToolset> where TToolset : Toolset
{
    internal ToolInclusion()
    {
    }

    public ToolInclusion<TToolset> Include(Func<ToolList<TToolset>, ToolName> tool) => this;
}

/// <summary>
/// A use that offers every tool but those it excludes.
/// </summary>
public sealed class ToolExclusion<TToolset> where TToolset : Toolset
{
    internal ToolExclusion()
    {
    }

    public ToolExclusion<TToolset> Exclude(Func<ToolList<TToolset>, ToolName> tool) => this;
}

/// <summary>
/// An agent's use of an MCP server.
/// </summary>
public sealed class McpServerUse
{
    internal McpServerUse()
    {
    }

    /// <summary>
    /// Pauses a run that calls any of the server's tools until the host approves or rejects the call.
    /// </summary>
    public McpServerUse RequiresApproval() => this;

    /// <summary>
    /// Marks every tool of the server as reading only, so plan mode offers them.
    /// </summary>
    public McpServerUse ReadOnly() => this;
}

/// <summary>
/// An agent's use of a sub-agent.
/// </summary>
public sealed class AgentUse
{
    internal AgentUse()
    {
    }

    /// <summary>
    /// Starts the sub-agent and returns at once; its answer reaches the calling run as a message before a later model
    /// call. The calling run does not end while its sub-agents work.
    /// </summary>
    public AgentUse InBackground() => this;
}

/// <summary>
/// The run options an agent starts every run from, and those that may change at runtime.
/// </summary>
public sealed class RunOptionsBuilder
{
    internal RunOptionsBuilder()
    {
    }

    /// <summary>
    /// The mode runs start in; <see cref="AgentMode.Default"/> unless set.
    /// </summary>
    public RunOptionsBuilder WithMode(AgentMode mode) => this;

    /// <summary>
    /// The most model calls one run's tool loop makes; 40 unless set.
    /// </summary>
    public RunOptionsBuilder WithMaxToolIterations(int iterations) => this;

    /// <summary>
    /// The most tool calls in a row that may fail before the run ends; 3 unless set.
    /// </summary>
    public RunOptionsBuilder WithMaxConsecutiveToolErrors(int errors) => this;

    /// <summary>
    /// Runs the tool calls of one model response at the same time instead of one after the other.
    /// </summary>
    public RunOptionsBuilder WithConcurrentToolCalls(bool concurrent) => this;

    /// <summary>
    /// Turns compaction on: before a model call, when the last call's input reached this many tokens (as the provider
    /// reported them), the history is compacted down to <see cref="WithTargetInputTokens"/>.
    /// </summary>
    public RunOptionsBuilder WithMaxInputTokens(int tokens) => this;

    /// <summary>
    /// How small compaction makes the history; half of <see cref="WithMaxInputTokens"/> unless set.
    /// </summary>
    public RunOptionsBuilder WithTargetInputTokens(int tokens) => this;

    /// <summary>
    /// Summarizes, with this model, what clearing old tool results did not free; without it, the oldest turns are
    /// dropped instead.
    /// </summary>
    public RunOptionsBuilder WithSummaryModel(string model) => this;

    /// <summary>
    /// Lets a run option change at runtime. The lambda never runs.
    /// </summary>
    /// <typeparam name="TOption">The option's type.</typeparam>
    /// <param name="option">The option, <c>r => r.MaxToolIterations</c>.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder Configurable<TOption>(Func<RunOptions, TOption> option) => this;
}

/// <summary>
/// How an agent's runs go; the options <see cref="RunOptionsBuilder"/> sets, as configurable picks name them.
/// </summary>
public sealed record RunOptions
{
    public AgentMode Mode { get; init; }

    public int MaxToolIterations { get; init; } = 40;

    public int MaxConsecutiveToolErrors { get; init; } = 3;

    public bool ConcurrentToolCalls { get; init; }

    public int? MaxInputTokens { get; init; }

    public int? TargetInputTokens { get; init; }

    public string? SummaryModel { get; init; }
}

/// <summary>
/// How a run treats the tools it may not run unasked.
/// </summary>
public enum AgentMode
{
    /// <summary>
    /// Tools that require approval ask the host, unless a standing approval or a rule decides.
    /// </summary>
    Default,

    /// <summary>
    /// Only tools marked read-only are offered. The model researches, then presents a plan with
    /// <c>exit_plan_mode</c>, which asks the host; approving it continues in the mode the host chooses.
    /// </summary>
    Plan,

    /// <summary>
    /// Tools that require approval run without asking, unless a rule rejects the call.
    /// </summary>
    Auto
}
