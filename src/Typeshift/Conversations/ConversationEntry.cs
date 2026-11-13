using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// One step of a conversation, appended as it happens: the input of a run, a model response, tool results, or the end
/// of a run. A conversation is its entries in order; a run whose entries have no ending one was interrupted, unless it
/// is still going on, in this process or another.
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
    // D78: the content types Microsoft.Extensions.AI's contract writes; one of any other type fails the whole entry
    private static readonly HashSet<Type>? WritableContents =
        Contract<AIContent>().PolymorphismOptions is { } contents
            ? [typeof(AIContent), .. contents.DerivedTypes.Select(content => content.DerivedType)]
            : null;

    /// <summary>
    /// The model that wrote the messages, for a model response.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// The tokens of the model call, for a model response.
    /// </summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>
    /// What compaction estimated the model call's input at, in tokens, for a model response of a run with a maximum
    /// of input tokens. With the input tokens of <see cref="Usage"/>, it corrects the estimate of the next run.
    /// </summary>
    public long? EstimatedInputTokens { get; init; }

    /// <summary>
    /// Whether the run ended with this entry: answered, failed, cancelled or waiting for your app's decision.
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
    /// The background sub-agents still working, on a summary: each call's id with the agent it called.
    /// </summary>
    public IReadOnlyDictionary<string, string>? BackgroundCalls { get; init; }

    // D78: how many stored contents this version of Microsoft.Extensions.AI could not read, which the entry leaves out
    internal int UnreadContents { get; init; }

    /// <inheritdoc />
    public static string Kind => "conversation_entry";

    /// <inheritdoc />
    public static int SchemaVersion => 1;

    /// <inheritdoc />
    public string Serialize() => StorableJson.Write(Write);

    /// <inheritdoc />
    public static ConversationEntry Deserialize(string json) => StorableJson.Read<ConversationEntry>(json, TryRead);

    void IJsonStorable<ConversationEntry>.WriteJson(Utf8JsonWriter writer) => Write(writer);

    static bool IJsonStorable<ConversationEntry>.TryReadJson(JsonElement element, out ConversationEntry value) =>
        TryRead(element, out value);

    private void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        StorableJson.WriteSchemaVersion(writer, SchemaVersion);
        writer.WriteString("agent", Agent);
        writer.WriteString("at", At);
        if (Model is not null) writer.WriteString("model", Model);

        if (Usage is not null)
        {
            writer.WritePropertyName("usage");
            JsonSerializer.Serialize(writer, Usage, Contract<UsageDetails>());
        }

        if (EstimatedInputTokens is { } estimated) writer.WriteNumber("estimatedInputTokens", estimated);
        if (EndsRun) writer.WriteBoolean("endsRun", true);
        if (Mode is { } mode) writer.WriteString("mode", mode.ToString());
        if (IsSummary) writer.WriteBoolean("isSummary", true);

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

        StorableJson.EnsureSchemaVersion(root, SchemaVersion, Kind);
        var messages = new List<ChatMessage>();
        var unread = 0;

        if (StorableJson.Member(root, "messages") is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var stored in array.EnumerateArray())
            {
                if (ReadMessage(stored, ref unread) is { } message) messages.Add(message);
            }
        }

        var estimate = StorableJson.Member(root, "estimatedInputTokens");
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
            EstimatedInputTokens = StorableJson.TryReadNumber(estimate, out long estimated) ? estimated : null,
            EndsRun = StorableJson.TryRead(StorableJson.Member(root, "endsRun"), out bool ends) && ends,
            Mode = StorableJson.TryReadEnum(StorableJson.Member(root, "mode"), out AgentMode mode) ? mode : null,
            IsSummary = StorableJson.TryRead(StorableJson.Member(root, "isSummary"), out bool summary) && summary,
            BackgroundCalls = StorableJson.TryReadMap<string>(background, StorableJson.TryRead, out var calls)
                ? calls
                : null,
            UnreadContents = unread
        };

        return true;
    }

    internal static bool CanWrite(AIContent content) =>
        WritableContents is null || WritableContents.Contains(content.GetType());

    // D78: a message whose contents do not all read is read content by content, and those this version of
    // Microsoft.Extensions.AI cannot read are left out, so one of them never costs the whole conversation
    private static ChatMessage? ReadMessage(JsonElement stored, ref int unread)
    {
        var contents = StorableJson.Member(stored, "contents");

        try
        {
            return stored.Deserialize(Contract<ChatMessage>())!;
        }
        catch (Exception exception) when (IsUnread(exception) && contents.ValueKind is JsonValueKind.Array)
        {
            var message = JsonSerializer.Deserialize(WithoutContents(stored), Contract<ChatMessage>())!;

            foreach (var content in contents.EnumerateArray())
            {
                if (TryReadContent(content) is { } read) message.Contents.Add(read);
                else unread++;
            }

            return message.Contents.Count > 0 ? message : null;
        }
    }

    private static AIContent? TryReadContent(JsonElement content)
    {
        try
        {
            return content.Deserialize(Contract<AIContent>());
        }
        catch (Exception exception) when (IsUnread(exception))
        {
            return null;
        }
    }

    private static bool IsUnread(Exception exception) => exception is JsonException or NotSupportedException;

    private static string WithoutContents(JsonElement message) =>
        StorableJson.Write(writer =>
        {
            writer.WriteStartObject();

            foreach (var member in message.EnumerateObject().Where(member => !member.NameEquals("contents")))
            {
                member.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

    // Microsoft.Extensions.AI's own contracts, which know every content type
    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> Contract<T>() =>
        (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
}
