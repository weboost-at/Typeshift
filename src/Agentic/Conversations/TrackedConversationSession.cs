using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

/// <summary>
/// A model exchange while it runs: the context sent to the model, and where it belongs.
/// </summary>
public class TrackedConversationSession
{
    internal TrackedConversationSession(ConversationStart start, StoredConversation? resumed, TimeProvider time)
    {
        Time = time;
        StartedAt = time.GetUtcNow();
        SessionId = Guid.CreateVersion7(StartedAt);
        ConversationId = start.ConversationId;
        Model = start.Model;
        ExecutionContext = resumed?.ExecutionContext ?? start.ExecutionContext;
        Messages = resumed is null ? [] : ResumableMessages(resumed.AggregatedMessages);
        UsageDetails = resumed?.AggregatedUsage ?? new UsageDetails();
        Input = start.Input;
        Resumed = resumed is not null;
    }

    /// <summary>
    /// Identifies the session while it runs.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// When the session started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// The conversation the session belongs to, as <see cref="Microsoft.Extensions.AI.ChatOptions.ConversationId"/>
    /// named it; null for a call outside of any conversation, which is tracked but never stored.
    /// </summary>
    public string? ConversationId { get; }

    /// <summary>
    /// The model the session runs on.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Where the conversation started: the agent's unique name, or <see cref="ChatExecutionContext.Unknown"/>.
    /// </summary>
    public ChatExecutionContext ExecutionContext { get; }

    /// <summary>
    /// The messages of the conversation's earlier sessions.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages { get; }

    /// <summary>
    /// The new input of this session.
    /// </summary>
    public IReadOnlyList<ChatMessage> Input { get; }

    /// <summary>
    /// Everything sent to the model: the earlier messages, then the new input.
    /// </summary>
    public IReadOnlyList<ChatMessage> Context => [.. Messages, .. Input];

    /// <summary>
    /// The usage of the conversation's earlier sessions.
    /// </summary>
    public UsageDetails UsageDetails { get; }

    /// <summary>
    /// Whether the session continues a stored conversation.
    /// </summary>
    public bool Resumed { get; }

    private protected TimeProvider Time { get; }

    // MEAI's response never contains the input, so the session stores it explicitly
    internal virtual Task<ConversationSession> CompleteAsync(ChatResponse response) =>
        Task.FromResult(new ConversationSession(
            Messages: [.. Input, .. response.Messages],
            Model: Model,
            UsageDetails: response.Usage ?? new UsageDetails(),
            FinishReason: response.FinishReason,
            StartedAt: StartedAt,
            FinishedAt: Time.GetUtcNow()));

    // A turn cancelled during a tool call leaves a call without a result, which providers reject; it is dropped
    private static IReadOnlyList<ChatMessage> ResumableMessages(IReadOnlyList<ChatMessage> messages)
    {
        var answered = messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.CallId)
            .ToHashSet();

        return
        [
            .. messages
                .Select(message => WithoutUnansweredCalls(message, answered))
                .Where(message => message.Contents.Count > 0)
        ];
    }

    private static ChatMessage WithoutUnansweredCalls(ChatMessage message, HashSet<string> answered)
    {
        bool IsUnanswered(AIContent content) => content is FunctionCallContent call && !answered.Contains(call.CallId);

        if (!message.Contents.Any(IsUnanswered)) return message;

        var resumable = message.Clone();
        resumable.Contents = [.. message.Contents.Where(content => !IsUnanswered(content))];
        return resumable;
    }
}
