using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// One step of a conversation, appended as it happens: the input of a run, a model response, tool results, or the end
/// of a run. A conversation is its entries in order; a run whose entries have no ending one was interrupted.
/// </summary>
/// <param name="Agent">The name of the agent the step belongs to.</param>
/// <param name="Messages">The step's messages; none for the end of a run that answered.</param>
/// <param name="At">When the step was stored.</param>
// D44: System.Text.Json and Newtonsoft.Json take entries as they are, in their stored JSON
[JsonConverter(typeof(StorableJsonConverter<ConversationEntry>))]
[TypeConverter(typeof(StorableTypeConverter<ConversationEntry>))]
public sealed record ConversationEntry(string Agent, IReadOnlyList<ChatMessage> Messages, DateTimeOffset At)
    : IStorable<ConversationEntry>, IJsonStorable<ConversationEntry>
{
    /// <summary>
    /// The model that wrote the messages, for a model response.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// The tokens of the model call, for a model response.
    /// </summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>
    /// Whether the run ended with this entry: answered, failed, cancelled or waiting for approval.
    /// </summary>
    public bool EndsRun { get; init; }

    /// <summary>
    /// The mode the run ended in, on its last entry, or was in, on a summary; a later run without a mode of its own
    /// continues in it.
    /// </summary>
    public AgentMode? Mode { get; init; }

    /// <summary>
    /// Whether the entry holds what compaction left of the history: a summary and the messages kept after it. A
    /// later run starts from the latest such entry.
    /// </summary>
    public bool IsSummary { get; init; }

    /// <summary>
    /// The tools approved for the rest of the conversation, on a summary, since a run reads none of the answers
    /// before it. Null on a summary an earlier version stored, whose runs load the whole conversation.
    /// </summary>
    public IReadOnlyList<string>? StandingApprovals { get; init; }

    /// <summary>
    /// The background sub-agents still working, on a summary: each call's id with the agent it called.
    /// </summary>
    public IReadOnlyDictionary<string, string>? BackgroundCalls { get; init; }

    /// <inheritdoc />
    public static string Kind => "conversation_entry";

    /// <inheritdoc />
    public static int Version => 1;

    /// <inheritdoc />
    public string Serialize() => StorableJson.Write(Write);

    /// <inheritdoc />
    public static ConversationEntry Deserialize(string json, int version)
    {
        if (version > Version)
            throw new JsonException($"Conversation entries of version {version} are newer than this code's {Version}.");

        return StorableJson.Read<ConversationEntry>(json, TryRead);
    }

    void IJsonStorable<ConversationEntry>.WriteJson(Utf8JsonWriter writer) => Write(writer);

    static bool IJsonStorable<ConversationEntry>.TryReadJson(JsonElement element, out ConversationEntry value) =>
        TryRead(element, out value);

    private void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("agent", Agent);
        writer.WriteString("at", At);
        if (Model is not null) writer.WriteString("model", Model);

        if (Usage is not null)
        {
            writer.WritePropertyName("usage");
            JsonSerializer.Serialize(writer, Usage, Contract<UsageDetails>());
        }

        if (EndsRun) writer.WriteBoolean("endsRun", true);
        if (Mode is { } mode) writer.WriteString("mode", mode.ToString());
        if (IsSummary) writer.WriteBoolean("isSummary", true);

        if (StandingApprovals is not null)
        {
            writer.WriteStartArray("standingApprovals");

            foreach (var tool in StandingApprovals)
            {
                writer.WriteStringValue(tool);
            }

            writer.WriteEndArray();
        }

        if (BackgroundCalls is not null)
        {
            writer.WriteStartObject("backgroundCalls");

            foreach (var (call, agent) in BackgroundCalls)
            {
                writer.WriteString(call, agent);
            }

            writer.WriteEndObject();
        }

        writer.WriteStartArray("messages");

        foreach (var message in Messages)
        {
            JsonSerializer.Serialize(writer, message, Contract<ChatMessage>());
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static bool TryRead(JsonElement root, out ConversationEntry entry)
    {
        entry = null!;
        if (root.ValueKind is not JsonValueKind.Object) return false;

        var messages = StorableJson.Member(root, "messages") is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(message => message.Deserialize(Contract<ChatMessage>())!).ToList()
            : [];

        var approvals = StorableJson.Member(root, "standingApprovals");
        var background = StorableJson.Member(root, "backgroundCalls");

        entry = new ConversationEntry(
            StorableJson.TryRead(StorableJson.Member(root, "agent"), out string agent) ? agent : "",
            messages,
            StorableJson.TryRead(StorableJson.Member(root, "at"), out DateTimeOffset at) ? at : default)
        {
            Model = StorableJson.TryRead(StorableJson.Member(root, "model"), out string model) ? model : null,
            Usage = StorableJson.Member(root, "usage") is { ValueKind: JsonValueKind.Object } usage
                ? usage.Deserialize(Contract<UsageDetails>())
                : null,
            EndsRun = StorableJson.TryRead(StorableJson.Member(root, "endsRun"), out bool ends) && ends,
            Mode = StorableJson.TryReadEnum(StorableJson.Member(root, "mode"), out AgentMode mode) ? mode : null,
            IsSummary = StorableJson.TryRead(StorableJson.Member(root, "isSummary"), out bool summary) && summary,
            StandingApprovals = StorableJson.TryReadList<string>(approvals, StorableJson.TryRead, out var tools)
                ? tools
                : null,
            BackgroundCalls = StorableJson.TryReadMap<string>(background, StorableJson.TryRead, out var calls)
                ? calls
                : null
        };

        return true;
    }

    // Microsoft.Extensions.AI's own contracts, which know every content type
    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> Contract<T>() =>
        (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
}
