using System.Text.Json;
using Agentic.Agents;
using Agentic.Json;
using Agentic.Persistence;

namespace Agentic.Conversations;

/// <summary>
/// A conversation as stored: every finished session, in order. The store keeps it under the conversation's id.
/// </summary>
/// <param name="ExecutionContext">Where the conversation started: the name of the agent that ran it.</param>
/// <param name="Sessions">The finished sessions, oldest first.</param>
/// <param name="LastActivity">When the latest session finished.</param>
/// <param name="CreatedAt">When the first session started.</param>
public sealed record StoredConversation(
    ChatExecutionContext ExecutionContext,
    IReadOnlyList<ConversationSession> Sessions,
    DateTimeOffset LastActivity,
    DateTimeOffset CreatedAt) : IStorable<StoredConversation>
{
    /// <inheritdoc />
    public string Serialize() => JsonSerializer.Serialize(this, LibraryJson.StoredConversation);

    /// <inheritdoc />
    public static StoredConversation Deserialize(string json) =>
        JsonSerializer.Deserialize(json, LibraryJson.StoredConversation)
        ?? throw new JsonException("A stored conversation must be a JSON object, not null.");
}
