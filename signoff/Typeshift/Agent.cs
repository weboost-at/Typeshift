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
    /// Offers another agent as a tool, named and described like the agent. A call runs it with the same id and no
    /// conversation, and returns its answer's text.
    /// </summary>
    /// <typeparam name="TAgent">The agent to call.</typeparam>
    protected void UseAgent<TAgent>() where TAgent : Agent
    {
    }

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
    /// Shortens the history a resumed conversation sends to the model.
    /// </summary>
    /// <returns>The chain of compaction options.</returns>
    protected CompactionBuilder WithCompaction() => new();

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
    /// Sets the model, as the host's <see cref="IChatClientProvider"/> names it.
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
}

/// <summary>
/// How an agent shortens a long conversation before it reaches the model. The stored conversation keeps everything.
/// </summary>
public sealed class CompactionBuilder
{
    internal CompactionBuilder()
    {
    }

    /// <summary>
    /// Sends at most this many of the latest messages, never splitting a tool call from its result; the instructions
    /// always go.
    /// </summary>
    public CompactionBuilder WithMaxMessages(int messages) => this;

    /// <summary>
    /// Summarizes the messages that no longer fit with this model instead of dropping them. The summary is stored
    /// with the conversation, so it is written once.
    /// </summary>
    public CompactionBuilder WithSummaryModel(string model) => this;
}
