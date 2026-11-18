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
                .AddEndpointFilter<ChatOwnerFilter>()
                .AddEndpointFilter<ChatBusyFilter>();

            chat.MapPost("/messages", async (
                int chatId,
                ChatInput input,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                var response = await support
                    .WithSettingsId(customer.Id)
                    .WithConversationId(chatId)
                    .RunAsync(input.Text, ct);

                return ChatReply.From(response);
            });
            // end-snippet

            // begin-snippet: chat-stream-endpoint
            chat.MapPost("/messages/stream", async (
                int chatId,
                ChatInput input,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                var updates = support
                    .WithSettingsId(customer.Id)
                    .WithConversationId(chatId)
                    .RunStreamingAsync(input.Text, ct);

                return TypedResults.ServerSentEvents(Events(await StartedAsync(updates, ct)));
            });
            // end-snippet

            // begin-snippet: chat-history-endpoint
            chat.MapGet("/messages", async (int chatId, AgentHost<SupportAgent> support, CancellationToken ct) =>
                ChatLine.From(await support.WithConversationId(chatId).LoadConversationAsync(ct)));
            // end-snippet

            // begin-snippet: chat-decisions-endpoint
            chat.MapGet("/decisions", async (int chatId, AgentHost<SupportAgent> support, CancellationToken ct) =>
                (await support.WithConversationId(chatId).GetDecisionsAsync(ct)).Open);
            // end-snippet

            // begin-snippet: chat-answer-endpoint
            chat.MapPost("/decisions", async Task<IResult> (
                int chatId,
                DecisionAnswer answer,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                try
                {
                    var resumed = await support
                        .WithSettingsId(customer.Id)
                        .WithConversationId(chatId)
                        .AnswerAsync([answer], ct);

                    // Its Decisions list what still waits
                    return TypedResults.Ok(ChatReply.From(resumed));
                }
                catch (DecisionClosedException)
                {
                    // Answered already, in another tab or by a retry, or passed by a newer message: the UI drops it
                    return TypedResults.Conflict();
                }
            });
            // end-snippet
        }
    }

    // begin-snippet: chat-stream-start
    private static async Task<IAsyncEnumerable<ChatResponseUpdate>> StartedAsync(
        IAsyncEnumerable<ChatResponseUpdate> updates,
        CancellationToken ct)
    {
        var reader = updates.GetAsyncEnumerator(ct);

        try
        {
            // The run starts with its first update: one that cannot start fails here, before the response begins
            return Rest(reader, await reader.MoveNextAsync());
        }
        catch
        {
            await reader.DisposeAsync();
            throw;
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Rest(
        IAsyncEnumerator<ChatResponseUpdate> reader,
        bool any)
    {
        await using (reader)
        {
            for (; any; any = await reader.MoveNextAsync())
            {
                yield return reader.Current;
            }
        }
    }
    // end-snippet

    // begin-snippet: chat-stream-events
    private static async IAsyncEnumerable<SseItem<object>> Events(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        await foreach (var update in updates)
        {
            if (update.Text is { Length: > 0 } text) yield return new SseItem<object>(text, "text");

            // Decisions come with the run's last update, so each one the UI shows can be answered
            foreach (var decision in update.Decisions)
            {
                yield return new SseItem<object>(decision, "decision");
            }
        }
    }
    // end-snippet
}
