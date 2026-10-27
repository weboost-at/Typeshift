using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// One run on its way to the model, as middleware, context providers and approval rules see it.
/// </summary>
public sealed class AgentRun
{
    internal AgentRun(
        Agent agent,
        IList<ChatMessage> messages,
        ChatOptions options,
        IServiceProvider services,
        AgentMode mode)
    {
        Agent = agent;
        Messages = messages;
        Options = options;
        Services = services;
        Mode = mode;
    }

    /// <summary>
    /// The agent that runs.
    /// </summary>
    public Agent Agent { get; }

    /// <summary>
    /// The messages the model receives: the resumed conversation and the input. Run middleware may change them.
    /// </summary>
    public IList<ChatMessage> Messages { get; internal set; }

    /// <summary>
    /// The resolved options. Run middleware may change them.
    /// </summary>
    public ChatOptions Options { get; }

    /// <summary>
    /// The run's services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// The mode the run is in; an approved plan switches it.
    /// </summary>
    public AgentMode Mode { get; internal set; }

    /// <summary>
    /// The response, once the run has one; run middleware that sets it without calling <c>next</c> answers in place
    /// of the model.
    /// </summary>
    public ChatResponse? Response { get; set; }

    // The run's invoke_agent span, when one is recorded (D36)
    internal System.Diagnostics.Activity? Activity { get; init; }

    // Where the input starts in the messages, after the resumed conversation; context goes in ahead of it (D31)
    internal int InputStart { get; init; }

    // The tools approved for the rest of the conversation, which a sub-agent's run inherits (D24, D33)
    internal HashSet<string> Standing { get; set; } = [];

    // What compaction did so far in this run, once it did anything (D32)
    internal Compaction? Compaction { get; set; }

    // The conversation's last model call before the run and what its provider reported, for compaction's estimate
    internal ReportedCall? LastCall { get; init; }

    // The model calls the run made so far, which the tool loop's rounds share (D32)
    internal int ModelCalls { get; set; }
}
