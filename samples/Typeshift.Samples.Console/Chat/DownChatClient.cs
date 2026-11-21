using System.Net;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// A provider that is down: every call fails with the status a provider answers then, which Typeshift tells as
// Unavailable without reflection
internal sealed class DownChatClient : IChatClient
{
    public const string Key = "down";

    private static readonly ChatClientMetadata Metadata = new("down", defaultModelId: "down-model");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new HttpRequestException("Service unavailable.", null, HttpStatusCode.ServiceUnavailable);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new HttpRequestException("Service unavailable.", null, HttpStatusCode.ServiceUnavailable);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? Metadata : null;

    public void Dispose()
    {
    }
}
