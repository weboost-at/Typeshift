using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Docs.Chat;

// Stands in for a provider: it answers every request with the name of its model
internal sealed class CannedChatClient(string model) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Answered by {model}."))
        {
            ModelId = model,
            FinishReason = ChatFinishReason.Stop,
            Usage = new UsageDetails
            {
                InputTokenCount = 100,
                OutputTokenCount = 20,
                TotalTokenCount = 120
            }
        });

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await GetResponseAsync(messages, options, ct);

        foreach (var update in response.ToChatResponseUpdates()) yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
