using Typeshift.Agents;
using Typeshift.Conversations;
using Typeshift.Persistence;
using Typeshift.Tests.Toolsets.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Conversations.Support;

// The scripted client under conversation tracking, with a conversation store the test controls
public abstract class ConversationTestBase : HostTestBase
{
    protected ConversationTestData Conversation => Data.Conversation;

    protected ConversationTracker Tracker => Services.GetRequiredService<ConversationTracker>();

    protected ConversationTestStore Store =>
        (ConversationTestStore)Services.GetRequiredService<ITypeshiftStore<Guid>>();

    // A fresh instance per read, composing a response sums usage into the instance it is given
    protected UsageDetails Usage =>
        new()
        {
            InputTokenCount = Conversation.InputTokens,
            OutputTokenCount = Conversation.OutputTokens
        };

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddTypeshiftStore<ConversationTestStore, Guid>();

    // Tracking outside the function loop, as AgentRuntime composes it
    protected IChatClient Tracked() =>
        ChatClient
            .AsBuilder()
            .UseConversationTracking(Conversation.Model)
            .UseFunctionInvocation()
            .Build(Services);

    protected ChatOptions Options(Guid? conversationId = null) =>
        new()
        {
            ConversationId = conversationId?.ToString(),
            Tools = [.. new ProbeToolset().CreateTools(Json)],
            AdditionalProperties = new AdditionalPropertiesDictionary().WithExecutionContext(new(Conversation.Context))
        };

    // Updates of one model call share a message id, as a streamed completion does
    protected IReadOnlyList<ChatResponseUpdate> AnswerUpdates()
    {
        var messageId = Guid.NewGuid().ToString("N");

        return
        [
            .. Conversation.AnswerDeltas.Select(delta => Update(messageId, new TextContent(delta))),
            Update(messageId, new UsageContent(Usage), ChatFinishReason.Stop)
        ];
    }

    protected IReadOnlyList<ChatResponseUpdate> CallUpdates()
    {
        var messageId = Guid.NewGuid().ToString("N");
        var arguments = new Dictionary<string, object?> { ["query"] = Conversation.Question };
        var call = new FunctionCallContent(Conversation.CallId, "probe", arguments);

        return
        [
            Update(messageId, call),
            Update(messageId, new UsageContent(Usage), ChatFinishReason.ToolCalls)
        ];
    }

    // Time moves by the pause after each update, while the consumer holds it
    protected async Task<IReadOnlyList<ReceivedUpdate>> ReceiveAsync(
        IAsyncEnumerable<ChatResponseUpdate> stream,
        TimeSpan pause = default)
    {
        List<ReceivedUpdate> received = [];

        await foreach (var update in stream)
        {
            received.Add(new ReceivedUpdate(update, Tracker.RealtimeSessions.Single().Updates));
            Time.Advance(pause);
        }

        return received;
    }

    // A subscriber that signals it was entered, then never finishes
    protected static Task Hang(SemaphoreSlim entered)
    {
        entered.Release();
        return new TaskCompletionSource().Task;
    }

    protected async Task<StoredConversation> StoredAsync(Guid id) =>
        await Store.LoadAsync<StoredConversation>(id, Ct)
        ?? throw new InvalidOperationException($"No conversation {id} is stored.");

    private ChatResponseUpdate Update(string messageId, AIContent content, ChatFinishReason? finishReason = null) =>
        new(ChatRole.Assistant, [content])
        {
            MessageId = messageId,
            ResponseId = Conversation.ResponseId,
            ModelId = Conversation.Model,
            FinishReason = finishReason
        };
}
