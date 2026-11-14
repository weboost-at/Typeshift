using System.Text.Json;
using System.Text.Json.Serialization;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// What a run asks your app before it goes on, one record per waiting call of the model: an
/// <see cref="ApprovalDecision"/> for a call of a tool that requires approval, a <see cref="PlanDecision"/> for a plan,
/// a <see cref="QuestionDecision"/> for questions to the user. Answer it by its <see cref="Id"/>. It writes and reads
/// its own JSON, so an endpoint returns it as it is.
/// </summary>
// D80: a decision is a stored record with an id of its own, never a content of the conversation's history
[JsonConverter(typeof(StorableJsonConverter<Decision>))]
public abstract record Decision : IJsonStorable<Decision>
{
    private protected Decision(string id, string agent, DateTimeOffset askedAt)
    {
        Id = id;
        Agent = agent;
        AskedAt = askedAt;
    }

    /// <summary>
    /// Names the decision in every answer. It is made when the run asks, never from the model's call id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The name of the agent that asked.
    /// </summary>
    public string Agent { get; }

    /// <summary>
    /// When the run asked, so your app can tell how long a decision has waited.
    /// </summary>
    public DateTimeOffset AskedAt { get; }

    private protected abstract string Kind { get; }

    /// <summary>
    /// Rejects the call or the plan, or declines the questions. The model reads that as the call's result and goes
    /// on.
    /// </summary>
    /// <param name="reason">Why, for the model; null for none.</param>
    /// <returns>The answer, for <c>AnswerAsync</c> or <c>RecordAnswersAsync</c>.</returns>
    public DecisionAnswer Reject(string? reason = null) => DecisionAnswer.Reject(Id, reason);

    void IJsonStorable<Decision>.WriteJson(Utf8JsonWriter writer) => Write(writer);

    static bool IJsonStorable<Decision>.TryReadJson(JsonElement element, out Decision value) =>
        TryRead(element, out value);

    private protected void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Kind);
        writer.WriteString("id", Id);
        writer.WriteString("agent", Agent);
        writer.WriteString("askedAt", AskedAt);
        WriteMembers(writer);
        writer.WriteEndObject();
    }

    private protected abstract void WriteMembers(Utf8JsonWriter writer);

    private static bool TryRead(JsonElement element, out Decision value)
    {
        value = null!;
        if (element.ValueKind is not JsonValueKind.Object) return false;

        value = Text(element, "kind") switch
        {
            ApprovalDecision.KindName => ApprovalDecision.Read(element),
            PlanDecision.KindName => PlanDecision.Read(element),
            QuestionDecision.KindName => QuestionDecision.Read(element),
            var kind => throw new JsonException($"'{kind}' is no kind of decision.")
        };

        return true;
    }

    private protected static string Text(JsonElement element, string member) =>
        StorableJson.TryRead(StorableJson.Member(element, member), out string text) ? text : "";

    private protected static DateTimeOffset Time(JsonElement element, string member) =>
        StorableJson.TryRead(StorableJson.Member(element, member), out DateTimeOffset at) ? at : default;
}

/// <summary>
/// A call of a tool that requires approval, which no rule, standing approval or mode decided: the tool and the
/// arguments the model sent, which are the arguments the call runs with once approved.
/// </summary>
[JsonConverter(typeof(StorableJsonConverter<ApprovalDecision>))]
public sealed record ApprovalDecision : Decision, IJsonStorable<ApprovalDecision>
{
    internal const string KindName = "approval";

    internal ApprovalDecision(
        string id,
        string agent,
        DateTimeOffset askedAt,
        string tool,
        string? server,
        JsonElement arguments)
        : base(id, agent, askedAt)
    {
        Tool = tool;
        Server = server;
        Arguments = arguments;
    }

    /// <summary>
    /// The tool's name.
    /// </summary>
    public string Tool { get; }

    /// <summary>
    /// The MCP server that offers the tool, as the agent names it; null for every other tool.
    /// </summary>
    public string? Server { get; }

    /// <summary>
    /// The arguments as the model sent them, a JSON object. The approved call runs with exactly these.
    /// </summary>
    public JsonElement Arguments { get; }

    private protected override string Kind => KindName;

    /// <summary>
    /// Approves the call, which runs once every decision that waits with it is answered.
    /// </summary>
    /// <returns>The answer, for <c>AnswerAsync</c> or <c>RecordAnswersAsync</c>.</returns>
    public DecisionAnswer Approve() => DecisionAnswer.Approve(Id);

    /// <summary>
    /// Approves the call and every later call of the same tool in the conversation: a standing approval, kept with
    /// the conversation, so it holds across runs and processes.
    /// </summary>
    /// <returns>The answer, for <c>AnswerAsync</c> or <c>RecordAnswersAsync</c>.</returns>
    public DecisionAnswer ApproveForConversation() => DecisionAnswer.ApproveForConversation(Id);

    void IJsonStorable<ApprovalDecision>.WriteJson(Utf8JsonWriter writer) => Write(writer);

    static bool IJsonStorable<ApprovalDecision>.TryReadJson(JsonElement element, out ApprovalDecision value)
    {
        value = element.ValueKind is JsonValueKind.Object ? Read(element) : null!;
        return value is not null;
    }

    internal static ApprovalDecision Read(JsonElement element) =>
        new(
            Text(element, "id"),
            Text(element, "agent"),
            Time(element, "askedAt"),
            Text(element, "tool"),
            StorableJson.TryRead(StorableJson.Member(element, "server"), out string server) ? server : null,
            StorableJson.Member(element, "arguments") is { ValueKind: JsonValueKind.Object } arguments
                ? arguments.Clone()
                : BatchCall.NoArguments);

    private protected override void WriteMembers(Utf8JsonWriter writer)
    {
        writer.WriteString("tool", Tool);
        if (Server is not null) writer.WriteString("server", Server);
        writer.WritePropertyName("arguments");
        Arguments.WriteTo(writer);
    }
}

/// <summary>
/// The plan a run in plan mode presents with <c>exit_plan_mode</c>, for your app to approve or reject.
/// </summary>
[JsonConverter(typeof(StorableJsonConverter<PlanDecision>))]
public sealed record PlanDecision : Decision, IJsonStorable<PlanDecision>
{
    internal const string KindName = "plan";

    internal PlanDecision(string id, string agent, DateTimeOffset askedAt, string plan)
        : base(id, agent, askedAt) =>
        Plan = plan;

    /// <summary>
    /// The plan, in Markdown, as the model wrote it.
    /// </summary>
    public string Plan { get; }

    private protected override string Kind => KindName;

    /// <summary>
    /// Approves the plan. The conversation leaves plan mode and continues in <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode"><see cref="AgentMode.Default"/> or <see cref="AgentMode.Auto"/>.</param>
    /// <returns>The answer, for <c>AnswerAsync</c> or <c>RecordAnswersAsync</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mode"/> is <see cref="AgentMode.Plan"/>, which an approved plan leaves.
    /// </exception>
    public DecisionAnswer Approve(AgentMode mode) => DecisionAnswer.ApprovePlan(Id, mode);

    void IJsonStorable<PlanDecision>.WriteJson(Utf8JsonWriter writer) => Write(writer);

    static bool IJsonStorable<PlanDecision>.TryReadJson(JsonElement element, out PlanDecision value)
    {
        value = element.ValueKind is JsonValueKind.Object ? Read(element) : null!;
        return value is not null;
    }

    internal static PlanDecision Read(JsonElement element) =>
        new(Text(element, "id"), Text(element, "agent"), Time(element, "askedAt"), Text(element, "plan"));

    private protected override void WriteMembers(Utf8JsonWriter writer) => writer.WriteString("plan", Plan);
}

/// <summary>
/// Your app's answer to one decision, by the decision's id. A UI posts it as JSON, such as
/// <c>{"decisionId":"...","approved":true}</c>, or <c>{"decisionId":"...","answers":[{"selected":["Dapper"]}]}</c> for
/// questions. It is checked against the stored decision when it is recorded.
/// </summary>
/// <param name="DecisionId">The <see cref="Decision.Id"/> of the decision it answers.</param>
public sealed record DecisionAnswer(string DecisionId)
{
    /// <summary>
    /// Whether the call or the plan is approved; false rejects it. Questions are answered by <see cref="Answers"/>.
    /// </summary>
    public bool Approved { get; init; }

    /// <summary>
    /// Why the call or the plan is rejected or the questions are declined, for the model; null for none.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// For an approved tool call: whether every later call of the tool in the conversation is approved too.
    /// </summary>
    public bool ForConversation { get; init; }

    /// <summary>
    /// For an approved plan: the mode the conversation continues in, <see cref="AgentMode.Default"/> when null.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AgentMode>))]
    public AgentMode? Mode { get; init; }

    /// <summary>
    /// For questions: one answer for each question, in the questions' order. Null declines them.
    /// </summary>
    public IReadOnlyList<QuestionAnswer>? Answers { get; init; }

    /// <summary>
    /// Approves a tool call.
    /// </summary>
    /// <param name="decisionId">The decision's id.</param>
    /// <returns>The answer.</returns>
    public static DecisionAnswer Approve(string decisionId) => new(decisionId) { Approved = true };

    /// <summary>
    /// Approves a tool call and every later call of the same tool in the conversation.
    /// </summary>
    /// <param name="decisionId">The decision's id.</param>
    /// <returns>The answer.</returns>
    public static DecisionAnswer ApproveForConversation(string decisionId) =>
        new(decisionId) { Approved = true, ForConversation = true };

    /// <summary>
    /// Approves a plan; the conversation continues in <paramref name="mode"/>.
    /// </summary>
    /// <param name="decisionId">The decision's id.</param>
    /// <param name="mode"><see cref="AgentMode.Default"/> or <see cref="AgentMode.Auto"/>.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mode"/> is <see cref="AgentMode.Plan"/>, which an approved plan leaves.
    /// </exception>
    public static DecisionAnswer ApprovePlan(string decisionId, AgentMode mode) =>
        mode is AgentMode.Default or AgentMode.Auto
            ? new(decisionId) { Approved = true, Mode = mode }
            : throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "An approved plan continues in the default or the auto mode.");

    /// <summary>
    /// Answers questions.
    /// </summary>
    /// <param name="decisionId">The decision's id.</param>
    /// <param name="answers">
    /// One answer for each question, in the questions' order. A string is an answer too: an option when it equals
    /// the option's label, the user's own words otherwise.
    /// </param>
    /// <returns>The answer.</returns>
    public static DecisionAnswer Answer(string decisionId, params IReadOnlyList<QuestionAnswer> answers) =>
        new(decisionId) { Approved = true, Answers = answers };

    /// <summary>
    /// Rejects a tool call or a plan, or declines questions.
    /// </summary>
    /// <param name="decisionId">The decision's id.</param>
    /// <param name="reason">Why, for the model; null for none.</param>
    /// <returns>The answer.</returns>
    public static DecisionAnswer Reject(string decisionId, string? reason = null) =>
        new(decisionId) { Reason = reason };

    /// <summary>
    /// Whether two answers say the same about the same decision, the answers to questions compared one by one.
    /// </summary>
    /// <param name="other">The other answer.</param>
    /// <returns>True when they are the same answer.</returns>
    public bool Equals(DecisionAnswer? other) =>
        other is not null
        && DecisionId == other.DecisionId
        && Approved == other.Approved
        && Reason == other.Reason
        && ForConversation == other.ForConversation
        && Mode == other.Mode
        && (Answers, other.Answers) switch
        {
            (null, null) => true,
            ({ } own, { } others) => own.SequenceEqual(others),
            _ => false
        };

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(DecisionId, Approved, Reason, ForConversation, Mode, Answers?.Count);
}

/// <summary>
/// A tool your app approved for a conversation: its later calls there run without asking.
/// </summary>
/// <param name="Tool">The tool's name.</param>
/// <param name="Server">The MCP server that offers the tool, as the agent names it; null for every other tool.</param>
/// <param name="GrantedAt">When the approval took effect: the moment a run began to carry it out.</param>
public sealed record StandingApproval(string Tool, string? Server, DateTimeOffset GrantedAt);

/// <summary>
/// What a conversation waits for, read in one load of its decisions: what a page shows after a reload.
/// </summary>
/// <param name="Open">The decisions your app has not answered yet.</param>
/// <param name="CanResume">
/// Whether every decision is answered and the answers are not carried out yet, so <c>ResumeAsync</c> continues the run.
/// </param>
/// <param name="Standing">The tools approved for the conversation.</param>
public sealed record ConversationDecisions(
    IReadOnlyList<Decision> Open,
    bool CanResume,
    IReadOnlyList<StandingApproval> Standing);
