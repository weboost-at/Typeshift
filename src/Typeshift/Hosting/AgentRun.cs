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

    // Where the input starts in the messages, after the resumed conversation; context goes in ahead of it (D31)
    internal int InputStart { get; init; }

    // The model calls the run made so far, which the tool loop's rounds share (D32)
    internal int ModelCalls { get; set; }
}
