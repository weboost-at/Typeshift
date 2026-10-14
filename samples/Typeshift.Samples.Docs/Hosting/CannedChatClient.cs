using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// Stands in for a provider's client, so the guide's scenarios run without a network: JSON when the run asks for it,
// otherwise one fixed answer
internal sealed class CannedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer(options))));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, Answer(options));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static string Answer(ChatOptions? options) =>
        options?.ResponseFormat is ChatResponseFormatJson
            ? """{ "Invoice": "INV-7", "State": "Approved", "Expected": "2026-11-02" }"""
            : "Your refund was approved and reaches your account within five days.";
}
