using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// One run on its way to the model, as middleware, context providers and approval rules see it.
/// </summary>
public sealed class AgentRun
{
    private ChatResponse? _response;

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
    /// The provider that serves the run: the key the call site, the agent's settings or its code name, a string, an
    /// enum value or another value of the agent's provider id type, or the key of the default provider; or the
    /// <see cref="ChatProvider"/> document the call site handed the run. Null for a run on the client registered for
    /// every agent. The run's client was taken for it before the run began, so it does not change.
    /// </summary>
    public object? Provider => ResolvedProvider?.Value;

    /// <summary>
    /// The run's services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// The mode the run is in, from its first model call to its last.
    /// </summary>
    public AgentMode Mode { get; }

    /// <summary>
    /// How the run goes, with every layer of settings applied: the limits of its tool loop and when its history is
    /// compacted. The mode it goes in is <see cref="Mode"/>, which the conversation's mode can change.
    /// </summary>
    public RunOptions RunOptions => Resolved.RunOptions;

    // D72: an id is read typed at the call, as the host was given it, and an id of another type is none
    /// <summary>
    /// Reads the settings id the run's host was given with <c>WithSettingsId</c>, the id of a customer for example. A
    /// sub-agent's run has its caller's.
    /// </summary>
    /// <param name="id">The id, when the run has one of <typeparamref name="TId"/>.</param>
    /// <typeparam name="TId">The id's type, as <c>WithSettingsId</c> was given it.</typeparam>
    /// <returns>Whether the run has a settings id of that type; false without one, or with another type.</returns>
    public bool TryGetSettingsId<TId>([MaybeNullWhen(false)] out TId id) where TId : notnull =>
        Read(Resolved.SettingsId, out id);

    /// <summary>
    /// Reads the id of the run's conversation: the one the host was given with <c>WithConversationId</c>, a new one
    /// when your app keeps every conversation, or on the raw path the <see cref="string"/> of
    /// <c>ChatOptions.ConversationId</c>.
    /// </summary>
    /// <param name="id">The id, when the run has one of <typeparamref name="TId"/>.</param>
    /// <typeparam name="TId">The id's type, as <c>WithConversationId</c> was given it.</typeparam>
    /// <returns>
    /// Whether the run has a conversation under an id of that type; false for a run without a conversation, such as
    /// a sub-agent's, or with an id of another type.
    /// </returns>
    public bool TryGetConversationId<TId>([MaybeNullWhen(false)] out TId id) where TId : notnull =>
        Read(Resolved.ConversationRun?.Conversation, out id);

    /// <summary>
    /// The response, once the run has one, for run middleware to read after <c>next</c> returned. A run middleware
    /// that sets it in place of calling <c>next</c> answers in place of the model.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// It is set after <c>next</c> was called, or by anything but run middleware. The answer was stored and streamed
    /// as it was made, so another one would reach only the caller of a run that does not stream.
    /// </exception>
    public ChatResponse? Response
    {
        get => _response;
        set => _response = Answerable
            ? value
            : throw new InvalidOperationException(
                "The run's response was set after next was called, or outside run middleware. An answer is stored, "
                + "streamed and published as it is made, so a response set afterwards would reach only the caller "
                + "of a run that does not stream. Set the response in place of calling next to answer in place of "
                + "the model, or wrap the provider's chat client in a DelegatingChatClient to change what a model "
                + "says.");
    }

    // D30: whether the code that runs now is a run middleware that has not called next, which may still answer in
    // place of the model
    internal bool Answerable { get; set; }

    // The answer of a pass as it was made, by the model, or none yet as a pass begins
    internal void Answered(ChatResponse? response) => _response = response;

    // D86: the provider with its type, which the entries of the run's model responses name as text
    internal ProviderId? ResolvedProvider => (Options as ResolvedChatOptions)?.Provider;

    // The conversation's client begins every run with options of this type, which hold what else the run resolved
    private ResolvedChatOptions Resolved => (ResolvedChatOptions)Options;

    private static bool Read<TId>(StoreId? stored, [MaybeNullWhen(false)] out TId id) where TId : notnull
    {
        id = stored is StoreId<TId> typed ? typed.Id : default;
        return stored is StoreId<TId>;
    }

    // The run's invoke_agent span, when one is recorded (D36)
    internal System.Diagnostics.Activity? Activity { get; init; }

    // Where the input starts in the messages, after the resumed conversation; context goes in ahead of it (D31)
    internal int InputStart { get; init; }

    // The tools approved for the conversation, which a sub-agent's run inherits (D80, D33)
    internal IReadOnlyList<StandingApproval> Standing { get; init; } = [];

    // What compaction did so far in this run, once it did anything (D32)
    internal Compaction? Compaction { get; set; }

    // The conversation's last model call before the run that holds an estimate and its provider's report, which
    // calibrate compaction's first estimate (D32)
    internal ReportedCall? LastCall { get; init; }

    // The model calls the run made so far, which the tool loop's rounds share (D32)
    internal int ModelCalls { get; set; }

    // D33, D32: the tokens the run spent beside its own model calls, on its sub-agents' runs and compaction's summary
    // calls, in all and those no entry holds yet. Sub-agents called in one response finish side by side, and
    // background ones on threads of their own
    private readonly Lock _spending = new();
    private UsageDetails? _spent;
    private UsageDetails? _unstored;

    internal void AddUsage(UsageDetails? usage)
    {
        if (usage is null) return;

        lock (_spending)
        {
            (_spent ??= new()).Add(usage);
            (_unstored ??= new()).Add(usage);
        }
    }

    // What the response reports beside its model calls' usage
    internal UsageDetails? SpentBeside()
    {
        lock (_spending)
        {
            return _spent is null ? null : Copy(_spent);
        }
    }

    // What the run's next entry that is no model response holds, so the entries add up to what the run reports
    internal UsageDetails? TakeUnstoredUsage()
    {
        lock (_spending)
        {
            var unstored = _unstored;
            _unstored = null;

            return unstored;
        }
    }

    private static UsageDetails Copy(UsageDetails usage)
    {
        var copy = new UsageDetails();
        copy.Add(usage);

        return copy;
    }
}
