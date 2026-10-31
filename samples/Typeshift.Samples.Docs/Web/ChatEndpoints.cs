using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Web;

internal static class ChatEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapChats()
        {
            // begin-snippet: chat-endpoint
            var chat = app.MapGroup("/chats/{chatId:int}")
                .AddEndpointFilter<ChatOwnerFilter>();

            chat.MapPost("/messages", async (
                int chatId,
                ChatInput input,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                var response = await support.RunAsync(
                    input.Text,
                    settingsId: customer.Id,
                    conversationId: chatId,
                    ct: ct);

                return ChatReply.From(response);
            });
            // end-snippet

            // begin-snippet: chat-stream-endpoint
            chat.MapPost("/messages/stream", (
                int chatId,
                ChatInput input,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                var updates = support.RunStreamingAsync(
                    input.Text,
                    settingsId: customer.Id,
                    conversationId: chatId,
                    ct: ct);

                return TypedResults.ServerSentEvents(Events(updates));
            });
            // end-snippet

            // begin-snippet: chat-approval-endpoint
            chat.MapPost("/approvals", async Task<IResult> (
                int chatId,
                ApprovalAnswer[] answers,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                // No answers would drop every pending request
                if (answers.Length == 0) return TypedResults.BadRequest();

                var reply = new ChatMessage(ChatRole.User, [.. answers.Select(answer => answer.ToResponse())]);

                try
                {
                    var resumed = await support.RunAsync(
                        reply,
                        settingsId: customer.Id,
                        conversationId: chatId,
                        ct: ct);

                    return TypedResults.Ok(ChatReply.From(resumed));
                }
                catch (ApprovalClosedException)
                {
                    // Answered already, in another tab or by a retry, or passed by a newer message: the UI drops it
                    return TypedResults.Conflict();
                }
            });
            // end-snippet
        }
    }

    // begin-snippet: chat-stream-events
    private static async IAsyncEnumerable<SseItem<object>> Events(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        await foreach (var update in updates)
        {
            if (update.Text is { Length: > 0 } text) yield return new SseItem<object>(text, "text");

            foreach (var request in update.Contents.OfType<ToolApprovalRequestContent>())
            {
                if (PendingApproval.From(request) is { } approval)
                    yield return new SseItem<object>(approval, "approval");
            }
        }
    }
    // end-snippet
}
