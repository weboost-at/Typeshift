using System.Text.Json;
using Typeshift.Generated;

namespace Typeshift;

// D80: a conversation's decisions as its store keeps them: at most one batch, the tools approved for the
// conversation, and why the decisions of the last batch closed, so a late answer learns why it is late. Whether a
// decision is open, answered or carried out is read here, never from the conversation's messages
internal sealed record DecisionDocument
{
    public const string Kind = "decisions";

    public const int SchemaVersion = 1;

    public static readonly DecisionDocument Empty = new();

    // The conversation's mode, once a run or your app names one; null while it runs in the agent's own
    public AgentMode? Mode { get; init; }

    public IReadOnlyList<StandingApproval> Standing { get; init; } = [];

    public DecisionBatch? Batch { get; init; }

    public IReadOnlyList<ClosedDecision> Closed { get; init; } = [];

    // The decisions your app may still answer: those of an open batch that have no answer
    public IReadOnlyList<Decision> Open => Batch is { State: BatchState.Open } batch ? batch.Unanswered : [];

    public ConversationDecisions ToDecisions() => new(Open, Batch is { State: BatchState.Answered }, Standing);

    // Why a decision that is not in the batch is closed; older than the last batch, nothing remembers it
    public DecisionClosedReason ClosedReason(string decision) =>
        Closed.FirstOrDefault(closed => closed.Id == decision)?.Reason ?? DecisionClosedReason.Unknown;

    public string Serialize() => StorableJson.Write(Write);

    public static DecisionDocument Deserialize(string json) => StorableJson.Read<DecisionDocument>(json, TryRead);

    private void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        StorableJson.WriteSchemaVersion(writer, SchemaVersion);
        if (Mode is { } mode) writer.WriteString("mode", mode.ToString());

        writer.WriteStartArray("standing");

        foreach (var approval in Standing)
        {
            writer.WriteStartObject();
            writer.WriteString("tool", approval.Tool);
            if (approval.Server is not null) writer.WriteString("server", approval.Server);
            writer.WriteString("at", approval.GrantedAt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        if (Batch is not null)
        {
            writer.WritePropertyName("batch");
            Batch.Write(writer);
        }

        writer.WriteStartArray("closed");

        foreach (var closed in Closed)
        {
            writer.WriteStartObject();
            writer.WriteString("id", closed.Id);
            writer.WriteString("reason", DecisionJson.Name(closed.Reason));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static bool TryRead(JsonElement root, out DecisionDocument document)
    {
        document = null!;
        if (root.ValueKind is not JsonValueKind.Object) return false;

        StorableJson.EnsureSchemaVersion(root, SchemaVersion, Kind);

        document = new DecisionDocument
        {
            Mode = StorableJson.TryReadEnum(StorableJson.Member(root, "mode"), out AgentMode mode) ? mode : null,
            Standing = DecisionJson.List<StandingApproval>(root, "standing", TryReadStanding),
            Batch = DecisionBatch.TryRead(StorableJson.Member(root, "batch"), out var batch) ? batch : null,
            Closed = DecisionJson.List<ClosedDecision>(root, "closed", TryReadClosed)
        };

        return true;
    }

    private static bool TryReadStanding(JsonElement element, out StandingApproval approval)
    {
        approval = null!;
        if (!StorableJson.TryRead(StorableJson.Member(element, "tool"), out string tool)) return false;

        approval = new StandingApproval(
            tool,
            StorableJson.TryRead(StorableJson.Member(element, "server"), out string server) ? server : null,
            StorableJson.TryRead(StorableJson.Member(element, "at"), out DateTimeOffset at) ? at : default);

        return true;
    }

    private static bool TryReadClosed(JsonElement element, out ClosedDecision closed)
    {
        closed = null!;

        if (!StorableJson.TryRead(StorableJson.Member(element, "id"), out string id)
            || !DecisionJson.TryReadEnum(StorableJson.Member(element, "reason"), out DecisionClosedReason reason))
            return false;

        closed = new ClosedDecision(id, reason);
        return true;
    }
}

// All calls of one model response of which at least one waits for your app. They wait and settle together
internal sealed record DecisionBatch(
    string Agent,
    DateTimeOffset AskedAt,
    BatchState State,
    IReadOnlyList<BatchCall> Calls)
{
    public IEnumerable<Ask> Asks => Calls.Select(call => call.Ask).OfType<Ask>();

    public IEnumerable<string> Ids => Asks.Select(ask => ask.Id);

    public IReadOnlyList<Decision> Unanswered =>
        [.. Calls.Where(call => call.Ask is { Answer: null }).Select(call => call.ToDecision(this)!)];

    // A batch is told by its decisions, whose ids no other batch has
    public bool IsSame(DecisionBatch other) => Ids.SequenceEqual(other.Ids);

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("agent", Agent);
        writer.WriteString("askedAt", AskedAt);
        writer.WriteString("state", DecisionJson.Name(State));
        writer.WriteStartArray("calls");

        foreach (var call in Calls)
        {
            call.Write(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public static bool TryRead(JsonElement element, out DecisionBatch batch)
    {
        batch = null!;

        if (!DecisionJson.TryReadEnum(StorableJson.Member(element, "state"), out BatchState state))
            return false;

        batch = new DecisionBatch(
            StorableJson.TryRead(StorableJson.Member(element, "agent"), out string agent) ? agent : "",
            StorableJson.TryRead(StorableJson.Member(element, "askedAt"), out DateTimeOffset at) ? at : default,
            state,
            DecisionJson.List<BatchCall>(element, "calls", BatchCall.TryRead));

        return true;
    }
}

internal enum BatchState
{
    // Asked, and at least one decision has no answer
    Open,

    // Every decision is answered, and no run has begun to carry the answers out
    Answered,

    // A run began to carry the answers out: every call the batch would run counts as started
    Started,

    // New input passed the batch by; each call gets a result that says so
    Expired
}

// One call of the batch: with an ask when it waits for your app, with a rejection when a rule rejected it, bare when
// it runs with the batch. Name and arguments repeat the stored call, so the batch is listed and replayed from the
// document alone
internal sealed record BatchCall(string CallId, string Tool, JsonElement Arguments)
{
    public static readonly JsonElement NoArguments = JsonElement.Parse("{}");

    // The MCP server that offers the tool, which a standing approval of it names
    public string? Server { get; init; }

    public Ask? Ask { get; init; }

    public string? Rejected { get; init; }

    public Decision? ToDecision(DecisionBatch batch) =>
        Ask?.Kind switch
        {
            AskKind.Approval => new ApprovalDecision(Ask.Id, batch.Agent, batch.AskedAt, Tool, Server, Arguments),
            AskKind.Plan => new PlanDecision(Ask.Id, batch.Agent, batch.AskedAt, Plan),
            _ => null
        };

    // The plan exit_plan_mode presents; arguments of another shape show as they are
    private string Plan =>
        Arguments.ValueKind is JsonValueKind.Object
        && Arguments.TryGetProperty("plan", out var plan)
        && plan.ValueKind is JsonValueKind.String
            ? plan.GetString()!
            : Arguments.GetRawText();

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("callId", CallId);
        writer.WriteString("tool", Tool);
        if (Server is not null) writer.WriteString("server", Server);
        writer.WritePropertyName("arguments");
        Arguments.WriteTo(writer);

        if (Ask is not null)
        {
            writer.WritePropertyName("ask");
            Ask.Write(writer);
        }

        if (Rejected is not null) writer.WriteString("rejected", Rejected);
        writer.WriteEndObject();
    }

    public static bool TryRead(JsonElement element, out BatchCall call)
    {
        call = null!;

        if (!StorableJson.TryRead(StorableJson.Member(element, "callId"), out string callId)
            || !StorableJson.TryRead(StorableJson.Member(element, "tool"), out string tool))
            return false;

        var arguments = StorableJson.Member(element, "arguments") is { ValueKind: JsonValueKind.Object } stored
            ? stored.Clone()
            : NoArguments;

        call = new BatchCall(callId, tool, arguments)
        {
            Server = StorableJson.TryRead(StorableJson.Member(element, "server"), out string server) ? server : null,
            Ask = Ask.TryRead(StorableJson.Member(element, "ask"), out var ask) ? ask : null,
            Rejected = StorableJson.TryRead(StorableJson.Member(element, "rejected"), out string reason)
                ? reason
                : null
        };

        return true;
    }
}

// What a call waits for, by the id your app answers with, and the answer once it is recorded
internal sealed record Ask(string Id, AskKind Kind)
{
    public DecisionAnswer? Answer { get; init; }

    // D80: ids are made when a run asks, in time order, never from the model's call ids, which a provider may reuse
    public static Ask Create(AskKind kind, TimeProvider time) =>
        new(Guid.CreateVersion7(time.GetUtcNow()).ToString("N"), kind);

    // The answer as it is recorded: what does not apply to its kind is left out, so the same answer compares equal
    // however it was posted
    public DecisionAnswer Normalize(DecisionAnswer answer)
    {
        if (!answer.Approved)
            return new(Id) { Reason = string.IsNullOrWhiteSpace(answer.Reason) ? null : answer.Reason };

        return Kind switch
        {
            AskKind.Approval when answer.Mode is not null => throw new ArgumentException(
                $"The answer to {Id} approves a plan into a mode, and the decision is a tool call: approve it with "
                + "Approve() or ApproveForConversation().",
                "answers"),
            AskKind.Approval => new(Id) { Approved = true, ForConversation = answer.ForConversation },
            _ when answer.ForConversation => throw new ArgumentException(
                $"The answer to {Id} approves a tool for the conversation, and the decision is a plan: approve it "
                + "with Approve(mode).",
                "answers"),
            _ when answer.Mode is AgentMode.Plan => throw new ArgumentException(
                $"The answer to {Id} approves a plan into plan mode: an approved plan continues in the default or "
                + "the auto mode.",
                "answers"),
            _ => new(Id) { Approved = true, Mode = answer.Mode ?? AgentMode.Default }
        };
    }

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("id", Id);
        writer.WriteString("kind", DecisionJson.Name(Kind));

        if (Answer is not null)
        {
            writer.WriteStartObject("answer");
            writer.WriteBoolean("approved", Answer.Approved);
            if (Answer.Reason is not null) writer.WriteString("reason", Answer.Reason);
            if (Answer.ForConversation) writer.WriteBoolean("forConversation", true);
            if (Answer.Mode is { } mode) writer.WriteString("mode", mode.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    public static bool TryRead(JsonElement element, out Ask ask)
    {
        ask = null!;

        if (!StorableJson.TryRead(StorableJson.Member(element, "id"), out string id)
            || !DecisionJson.TryReadEnum(StorableJson.Member(element, "kind"), out AskKind kind))
            return false;

        ask = new Ask(id, kind)
        {
            Answer = StorableJson.Member(element, "answer") is { ValueKind: JsonValueKind.Object } answer
                ? ReadAnswer(id, answer)
                : null
        };

        return true;
    }

    private static DecisionAnswer ReadAnswer(string id, JsonElement answer) =>
        new(id)
        {
            Approved = StorableJson.TryRead(StorableJson.Member(answer, "approved"), out bool approved) && approved,
            Reason = StorableJson.TryRead(StorableJson.Member(answer, "reason"), out string reason) ? reason : null,
            ForConversation = StorableJson.TryRead(StorableJson.Member(answer, "forConversation"), out bool all) && all,
            Mode = StorableJson.TryReadEnum(StorableJson.Member(answer, "mode"), out AgentMode mode) ? mode : null
        };
}

internal enum AskKind
{
    Approval,
    Plan
}

internal sealed record ClosedDecision(string Id, DecisionClosedReason Reason);

// The document's enum members, in lower case, and its lists, which leave out what does not read
internal static class DecisionJson
{
    public static string Name<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    public static bool TryReadEnum<TEnum>(JsonElement element, out TEnum value) where TEnum : struct, Enum
    {
        value = default;

        return StorableJson.TryRead(element, out string text)
            && Enum.TryParse(text, ignoreCase: true, out value)
            && Enum.IsDefined(value);
    }

    public static IReadOnlyList<T> List<T>(JsonElement root, string member, StorableReader<T> read) =>
        StorableJson.TryReadList(StorableJson.Member(root, member), read, out var items) ? items : [];
}
