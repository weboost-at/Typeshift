using System.Text.Json;
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
public sealed record ConversationEntry(string Agent, IReadOnlyList<ChatMessage> Messages, DateTimeOffset At)
    : IStorable<ConversationEntry>
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
    /// Whether the run ended with this entry: answered, failed or cancelled.
    /// </summary>
    public bool EndsRun { get; init; }

    /// <inheritdoc />
    public static string Kind => "conversation_entry";

    /// <inheritdoc />
    public static int Version => 1;

    /// <inheritdoc />
    public string Serialize() =>
        StorableJson.Write(writer =>
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

            writer.WriteStartArray("messages");

            foreach (var message in Messages)
            {
                JsonSerializer.Serialize(writer, message, Contract<ChatMessage>());
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    /// <inheritdoc />
    public static ConversationEntry Deserialize(string json, int version)
    {
        if (version > Version)
            throw new JsonException($"Conversation entries of version {version} are newer than this code's {Version}.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind is not JsonValueKind.Object)
            throw new JsonException($"A conversation entry must be a JSON object, not {root.ValueKind}.");

        var messages = StorableJson.Member(root, "messages") is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(message => message.Deserialize(Contract<ChatMessage>())!).ToList()
            : [];

        return new ConversationEntry(
            StorableJson.TryRead(StorableJson.Member(root, "agent"), out string agent) ? agent : "",
            messages,
            StorableJson.TryRead(StorableJson.Member(root, "at"), out DateTimeOffset at) ? at : default)
        {
            Model = StorableJson.TryRead(StorableJson.Member(root, "model"), out string model) ? model : null,
            Usage = StorableJson.Member(root, "usage") is { ValueKind: JsonValueKind.Object } usage
                ? usage.Deserialize(Contract<UsageDetails>())
                : null,
            EndsRun = StorableJson.TryRead(StorableJson.Member(root, "endsRun"), out bool ends) && ends
        };
    }

    // Microsoft.Extensions.AI's own contracts, which know every content type
    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> Contract<T>() =>
        (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
}
