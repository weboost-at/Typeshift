using Microsoft.Extensions.AI;

namespace Typeshift;

// The options ResolveOptionsAsync returns remember what else the run resolved, so the raw path's client gets it too:
// the providers (D86), the run options, the settings id (D26), and once a run begins, the run and its conversation
internal sealed class ResolvedChatOptions : ChatOptions
{
    public ResolvedChatOptions(ChatOptions options)
        : base(options)
    {
    }

    public ProviderId? Provider { get; set; }

    // D86: null for the run's provider
    public ProviderId? SummaryProvider { get; set; }

    // D87: the provider the call site handed the run, which serves its whole run: its sub-agents, the agents it hands
    // off to, and its summary
    public ProviderId? HandedProvider { get; set; }

    public RunOptions RunOptions { get; set; } = new();

    // D52: a run whose settings name a mode switches the conversation to it
    public bool RunSetsMode { get; set; }

    public StoreId? SettingsId { get; set; }

    // D27: the settings the options were resolved from, in the order they applied, as the run's log entry names them
    public string Layers { get; set; } = "the options it was given";

    public StoreId? Conversation { get; set; }

    // D73: a run of the host's members, which the app continues with WithConversationId; the raw path names its
    // conversations in ChatOptions.ConversationId instead
    public bool HostRun { get; set; }

    // D50: a run without input that also continues a run that failed, was cancelled or stopped, whose input the
    // conversation holds already
    public bool Retries { get; set; }

    public AgentRun? AgentRun { get; set; }

    public ConversationRun? ConversationRun { get; set; }

    // D33: the run that called this one as a sub-agent
    public AgentRun? Caller { get; set; }

    // D34, D50: the claim on the conversation a handoff or the agent passing a run on handed this run
    public RunClaim? Claim { get; set; }

    // D34: the handoffs the run made before this agent took it over
    public int Handoffs { get; set; }

    // D34, D50: the conversation as the host's run loaded it when it took it, before these options resolved
    public IReadOnlyList<ConversationEntry>? Entries { get; set; }

    // D64: the format a typed run asked for, which an agent answering in its place answers in too
    public ChatResponseFormat? ResultFormat { get; set; }

    // D29: the conversation's client gives every call its run. A call without one came from a layer taken out of the
    // created client, and passing it on would decide no call, store nothing and count no model call
    public static ResolvedChatOptions OfRun(ChatOptions? options) =>
        options is ResolvedChatOptions { AgentRun: not null, ConversationRun: not null } resolved
            ? resolved
            : throw new InvalidOperationException(
                "A client taken out of the one CreateChatClientAsync created was called on its own: call the created "
                + "client, which begins the run its tool loop and checkpoints belong to.");

    // D28, D32: the first value a settings layer set that no run can go with, a blank provider or model or a run
    // option out of range, and what it must do instead; null when every value holds
    public (string Option, object Value, string Must)? GetInvalid() =>
        this switch
        {
            { Provider.Value: string provider } when IsBlank(provider) =>
                (nameof(Provider), $"'{provider}'", "name a provider"),
            { ModelId: { } model } when IsBlank(model) => ("Model", $"'{model}'", "name a model"),
            { SummaryProvider.Value: string provider } when IsBlank(provider) =>
                (nameof(SummaryProvider), $"'{provider}'", "name a provider"),
            _ => RunOptions.GetInvalid()
        };

    private static bool IsBlank(string text) => string.IsNullOrWhiteSpace(text);

    // D26: what a call on the raw path's client runs with, these being the options the client was created from. A
    // library calls a client without options or with options of its own, which would otherwise run the bare model:
    // those lay over these their conversation id, their response format and the tools they add, and the agent's
    // instructions, model, provider, run options and tools stay. Options resolved for this client's provider and
    // model replace these; for another they would reach a client that serves neither
    public ResolvedChatOptions ForCall(ChatOptions? options) =>
        options switch
        {
            null => this,
            ResolvedChatOptions resolved when Equals(resolved.Provider, Provider) && resolved.ModelId == ModelId =>
                resolved,
            ResolvedChatOptions resolved => throw new InvalidOperationException(
                $"This chat client serves {Served(Provider, ModelId)}, and the call's options were resolved for "
                + $"{Served(resolved.Provider, resolved.ModelId)}: create a client from these options with "
                + "CreateChatClientAsync."),
            _ => Under(options)
        };

    private ResolvedChatOptions Under(ChatOptions options)
    {
        // D72: the text of the host's conversation names it too, as an Agent Framework session names it by the id
        // the host's response gave
        if (Conversation is { } conversation
            && options.ConversationId is { } named
            && named != conversation.ToString())
        {
            throw new ArgumentException(
                $"This chat client continues the conversation '{conversation}' of the host it was created on, and "
                + $"the call's options name the conversation '{named}': leave ChatOptions.ConversationId out, or "
                + "create the client on a host without a conversation id, where it names the conversation.",
                nameof(options));
        }

        var merged = (ResolvedChatOptions)Clone();
        merged.ConversationId = options.ConversationId ?? ConversationId;
        merged.ResponseFormat = options.ResponseFormat ?? ResponseFormat;

        if (options.Tools?.Where(tool => Tools?.Contains(tool) is not true).ToList() is [_, ..] added)
            merged.Tools = [.. Tools ?? [], .. added];

        return merged;
    }

    private static string Served(ProviderId? provider, string? model) =>
        (provider, model) switch
        {
            (null, null) => "the default client's own model",
            (null, _) => $"'{model}' on the default client",
            (_, null) => $"'{provider}'",
            _ => $"'{model}' of '{provider}'"
        };

    public override ChatOptions Clone() =>
        new ResolvedChatOptions(this)
        {
            Provider = Provider,
            SummaryProvider = SummaryProvider,
            HandedProvider = HandedProvider,
            RunOptions = RunOptions,
            RunSetsMode = RunSetsMode,
            SettingsId = SettingsId,
            Layers = Layers,
            Conversation = Conversation,
            HostRun = HostRun,
            Retries = Retries,
            AgentRun = AgentRun,
            ConversationRun = ConversationRun,
            Caller = Caller,
            Claim = Claim,
            Handoffs = Handoffs,
            Entries = Entries,
            ResultFormat = ResultFormat
        };
}
