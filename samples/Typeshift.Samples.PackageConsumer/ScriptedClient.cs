using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.PackageConsumer;

// A model for a run without a network: it calls greet for Ada, then answers with what the tool returned. It keeps the
// temperature of its last call, so the consumer sees its stored settings reach the model
internal sealed class ScriptedClient : IChatClient
{
    public float? Temperature { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Temperature = options?.Temperature;

        var greet = new FunctionCallContent("call-1", "greet", new Dictionary<string, object?> { ["name"] = "Ada" });
        var answer = messages.Last().Contents.OfType<FunctionResultContent>().FirstOrDefault() is { } result
            ? new ChatMessage(ChatRole.Assistant, $"{result.Result}")
            : new ChatMessage(ChatRole.Assistant, [greet]);

        return Task.FromResult(new ChatResponse(answer));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);

        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
