using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// One step of a conversation, appended as it happens: the input of a run, a model response, tool results, or the end
/// of a run. A conversation is its entries in order; a run whose entries have no ending one was interrupted, unless it
/// is still going on, in this container or another.
/// </summary>
// D44: System.Text.Json and Newtonsoft.Json take entries as they are, in their stored JSON
[JsonConverter(typeof(StorableJsonConverter<ConversationEntry>))]
[TypeConverter(typeof(StorableTypeConverter<ConversationEntry>))]
public sealed record ConversationEntry : IStorable<ConversationEntry>, IJsonStorable<ConversationEntry>
{
    // D78: the content types Microsoft.Extensions.AI's contract writes; one of any other type fails the whole entry
    private static readonly HashSet<Type>? WritableContents =
        Contract<AIContent>().PolymorphismOptions is { } contents
            ? [typeof(AIContent), .. contents.DerivedTypes.Select(content => content.DerivedType)]
            : null;

    /// <summary>
    /// The name of the agent the step belongs to.
    /// </summary>
    public required string Agent { get; init; }

    /// <summary>
    /// The step's messages; none for the end of a run that answered.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>
    /// When the step was stored.
    /// </summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>
    /// The entry's number in its conversation: one past the highest the run that appended it had loaded or appended
    /// before. The store refuses the entry of a run that loaded the conversation before another run, in another
    /// container, appended under the number, as a unique key on the conversation and this number does, and the
    /// refused run stops (<see cref="IConversationStore{TId}.AppendConversationAsync"/>). A number can be skipped,
    /// after an append that failed, and numbers do not order the entries: the order they were appended in does. 0 for
    /// an entry stored before entries were numbered.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// The model that wrote the messages, for a model response.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// The provider whose client wrote the messages, for a model response: a key as text, or a provider document's
    /// name, else its kind. Null for one on the client registered for every agent, and on entries stored before
    /// entries named their provider.
    /// </summary>
    public string? Provider { get; init; }

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
    /// The mode of the conversation as an earlier version stored it, on a run's last entry and on a summary. A
    /// conversation's mode is now kept with its decisions, and this one holds only until they name one.
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

    // D45: the mark of a stored tool result that is content, not JSON
    internal const string ContentResult = "typeshift.content_result";

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
        if (Sequence > 0) writer.WriteNumber("sequence", Sequence);
        if (Model is not null) writer.WriteString("model", Model);
        if (Provider is not null) writer.WriteString("provider", Provider);

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
            JsonSerializer.Serialize(writer, MarkContentResults(message), Contract<ChatMessage>());
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

        var sequence = StorableJson.Member(root, "sequence");
        var estimate = StorableJson.Member(root, "estimatedInputTokens");
        var background = StorableJson.Member(root, "backgroundCalls");

        entry = new ConversationEntry
        {
            Agent = StorableJson.TryRead(StorableJson.Member(root, "agent"), out string agent) ? agent : "",
            Messages = messages,
            At = StorableJson.TryRead(StorableJson.Member(root, "at"), out DateTimeOffset at) ? at : default,
            // D50: an entry an earlier version stored has no number
            Sequence = StorableJson.TryReadNumber(sequence, out long numbered) ? numbered : 0,
            Model = StorableJson.TryRead(StorableJson.Member(root, "model"), out string model) ? model : null,
            Provider = StorableJson.TryRead(StorableJson.Member(root, "provider"), out string named) ? named : null,
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
            return ReadContentResults(stored.Deserialize(Contract<ChatMessage>())!);
        }
        catch (Exception exception) when (IsUnread(exception) && contents.ValueKind is JsonValueKind.Array)
        {
            var message = JsonSerializer.Deserialize(WithoutContents(stored), Contract<ChatMessage>())!;

            foreach (var content in contents.EnumerateArray())
            {
                if (TryReadContent(content) is { } read) message.Contents.Add(read);
                else unread++;
            }

            return message.Contents.Count > 0 ? ReadContentResults(message) : null;
        }
    }

    // D45: a tool's content, such as an image, goes to the model as content, in later runs too. Its JSON reads back as
    // JSON, which the model would receive as text, so the result carries a mark that has it read back as content
    private static ChatMessage MarkContentResults(ChatMessage message)
    {
        if (!message.Contents.Any(IsContentResult)) return message;

        var marked = message.Clone();
        marked.Contents = [.. message.Contents.Select(content => IsContentResult(content) ? Marked(content) : content)];
        return marked;
    }

    private static bool IsContentResult(AIContent content) =>
        content is FunctionResultContent { Result: AIContent or IEnumerable<AIContent> };

    private static FunctionResultContent Marked(AIContent content)
    {
        var result = (FunctionResultContent)content;

        return new FunctionResultContent(result.CallId, result.Result)
        {
            Exception = result.Exception,
            Annotations = result.Annotations,
            AdditionalProperties = new(result.AdditionalProperties ?? []) { [ContentResult] = true }
        };
    }

    private static ChatMessage ReadContentResults(ChatMessage message)
    {
        foreach (var result in message.Contents.OfType<FunctionResultContent>())
        {
            if (result is not { Result: JsonElement json, AdditionalProperties: { } properties }
                || !properties.Remove(ContentResult)) continue;

            if (properties.Count == 0) result.AdditionalProperties = null;
            result.Result = TryReadContentResult(json) ?? json;
        }

        return message;
    }

    // D78: a result whose content this version cannot read stays the JSON it was stored as
    private static object? TryReadContentResult(JsonElement json)
    {
        try
        {
            return json.ValueKind is JsonValueKind.Array
                ? json.Deserialize(Contract<IList<AIContent>>())
                : json.Deserialize(Contract<AIContent>());
        }
        catch (Exception exception) when (IsUnread(exception))
        {
            return null;
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
