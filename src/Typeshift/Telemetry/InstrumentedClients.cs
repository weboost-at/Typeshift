using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D36: one instrumented client per provider and model, kept as long as the host, whose calls each go to the client
// their run got. A source and a meter per client would reset the metrics whenever a provider hands out a new client,
// per run or per tenant, since the OpenTelemetry SDK ends the instruments of a disposed meter
internal sealed class InstrumentedClients : IDisposable
{
    private readonly ConcurrentDictionary<(object Provider, string Model), OpenTelemetryChatClient> _instrumented = [];

    // The client of the call going on; an async method's change to an AsyncLocal stays within its call
    private readonly AsyncLocal<IChatClient?> _call = new();

    // A run's pipeline disposes what it wraps, and neither the instrumented client nor the provider's is its to dispose
    public IChatClient Instrument(IChatClient client, object provider, string model)
    {
        var metadata = client.GetService<ChatClientMetadata>();
        var instrumented = _instrumented.GetOrAdd((provider, model), _ => CreateInstrumented(metadata));

        return new UndisposedChatClient(new CallingClient(instrumented, client, _call));
    }

    public void Dispose()
    {
        foreach (var instrumented in _instrumented.Values)
        {
            instrumented.Dispose();
        }
    }

    // Its metadata comes from the first client, as the instrumentation reads it once
    private OpenTelemetryChatClient CreateInstrumented(ChatClientMetadata? metadata) =>
        new(new CurrentClient(metadata, _call), sourceName: Telemetry.SourceName);

    private sealed class CallingClient(IChatClient instrumented, IChatClient client, AsyncLocal<IChatClient?> call)
        : DelegatingChatClient(instrumented)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            call.Value = client;
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            call.Value = client;

            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }
        }
    }

    // The client of the call going on, under the instrumentation every call of a provider and model shares
    private sealed class CurrentClient(ChatClientMetadata? metadata, AsyncLocal<IChatClient?> call) : IChatClient
    {
        private IChatClient Client =>
            call.Value ?? throw new InvalidOperationException("An instrumented client is called outside a run.");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Client.GetResponseAsync(messages, options, cancellationToken);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Client.GetStreamingResponseAsync(messages, options, cancellationToken);

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) && serviceKey is null
                ? metadata
                : call.Value?.GetService(serviceType, serviceKey);

        public void Dispose()
        {
        }
    }
}
