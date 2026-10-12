using System.Runtime.CompilerServices;
using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

// Records every model exchange of a conversation; ChatOptions.ConversationId names the conversation to resume
internal sealed class TrackedChatClient(IChatClient innerClient, ConversationTracker tracker, string model)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var session = await tracker.TrackSessionAsync(Start(messages, options), ct);

        try
        {
            var response = await base.GetResponseAsync(session.Context, ForProvider(options), ct);
            await tracker.EndSessionAsync(session.SessionId, response);
            return response;
        }
        catch (Exception exception)
        {
            await tracker.EndSessionAsync(session.SessionId, GetFailure(exception));
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var session = await tracker.TrackRealtimeSessionAsync(Start(messages, options), ct);

        // The finally also runs when the consumer stops early or the stream is cancelled
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(session.Context, ForProvider(options), ct))
            {
                session.Track(update);
                if (session.ShouldPublish) await session.PublishAsync();
                yield return update;
            }
        }
        finally
        {
            await session.PublishAsync();
            await tracker.EndSessionAsync(session.SessionId, session.Updates.ToChatResponse());
        }
    }

    private ConversationStart Start(IEnumerable<ChatMessage> messages, ChatOptions? options) =>
        new(
            ConversationId: Guid.TryParse(options?.ConversationId, out var id) ? id : null,
            Model: model,
            ExecutionContext: options?.AdditionalProperties?.GetExecutionContext() ?? ChatExecutionContext.Unknown,
            Input: messages as ChatMessage[] ?? [.. messages]);

    // The tracker resends the conversation itself, a provider would take the id for state of its own
    private static ChatOptions? ForProvider(ChatOptions? options)
    {
        if (options?.ConversationId is null) return options;

        var forProvider = options.Clone();
        forProvider.ConversationId = null;
        return forProvider;
    }

    private static ChatResponse GetFailure(Exception exception)
    {
        var error = exception switch
        {
            OperationCanceledException => new ErrorContent("The request was cancelled before the model answered."),
            _ => new ErrorContent("The provider call failed, the model did not answer.")
            {
                Details = exception.Message
            }
        };

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [error]));
    }
}
