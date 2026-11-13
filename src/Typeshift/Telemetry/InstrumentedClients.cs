using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D36: one instrumented client per backend, kept as long as the host, whose calls each go to the client their run
// got. A source and a meter per client would reset the metrics whenever a provider hands out a new client, per run or
// per tenant, since the OpenTelemetry SDK ends the instruments of a disposed meter
internal sealed class InstrumentedClients : IDisposable
{
    // Lazy, so two runs that meet a backend at once make one client and leave no second meter behind
    private readonly ConcurrentDictionary<
        (string? Provider, string? Address, int? Port, string? DefaultModel),
        Lazy<OpenTelemetryChatClient>> _instrumented = [];

    // The client of the call going on; an async method's change to an AsyncLocal stays within its call
    private readonly AsyncLocal<IChatClient?> _call = new();

    // A run's pipeline disposes what it wraps, and neither the instrumented client nor the provider's is its to dispose
    public IChatClient Instrument(IChatClient client, string? model)
    {
        var metadata = client.GetService<ChatClientMetadata>();

        // A client's own model names only the calls that name none, so the clients a provider makes per model share
        // their backend's instrumentation
        var backend = new ChatClientMetadata(
            metadata?.ProviderName,
            metadata?.ProviderUri,
            model is null ? metadata?.DefaultModelId : null);

        var instrumented = _instrumented.GetOrAdd(
            (backend.ProviderName, backend.ProviderUri?.Host, backend.ProviderUri?.Port, backend.DefaultModelId),
            _ => new Lazy<OpenTelemetryChatClient>(() => CreateInstrumented(backend)));

        return new UndisposedChatClient(new CallingClient(instrumented.Value, client, _call));
    }

    public void Dispose()
    {
        foreach (var instrumented in _instrumented.Values)
        {
            instrumented.Value.Dispose();
        }
    }

    // The instrumentation reads the provider's name, its address and the default model once, so the clients that
    // share one are those that agree on them, and every span and metric names the backend that answered
    private OpenTelemetryChatClient CreateInstrumented(ChatClientMetadata backend) =>
        new(new CurrentClient(backend, _call), sourceName: Telemetry.SourceName);

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

    // The client of the call going on, under the instrumentation every call to a backend shares
    private sealed class CurrentClient(ChatClientMetadata backend, AsyncLocal<IChatClient?> call) : IChatClient
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
                ? backend
                : call.Value?.GetService(serviceType, serviceKey);

        public void Dispose()
        {
        }
    }
}
