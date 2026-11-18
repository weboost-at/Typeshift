using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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

    // D45: a provider's client writes a JSON result indented and passes text on as it is, so the model gets each JSON
    // result as text without white space. Every layer above, the stored entries and the response keep the element
    private sealed class CallingClient(IChatClient instrumented, IChatClient client, AsyncLocal<IChatClient?> call)
        : DelegatingChatClient(instrumented)
    {
        // Each result once, though every later model call sends it again; weakly, so a client of the raw path that
        // your app keeps holds no result of a call it made
        private readonly ConditionalWeakTable<AIContent, AIContent> _sent = new();

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            call.Value = client;
            return await base.GetResponseAsync(AsSent(messages), options, cancellationToken);
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            call.Value = client;

            await foreach (var update in base.GetStreamingResponseAsync(AsSent(messages), options, cancellationToken))
            {
                yield return update;
            }
        }

        private static bool HoldsJson(ChatMessage message) => message.Contents.Any(IsJson);

        private static bool IsJson(AIContent content) =>
            content is FunctionResultContent
            {
                Result: JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array }
            };

        private static AIContent AsText(AIContent content)
        {
            var result = (FunctionResultContent)content;

            return new FunctionResultContent(result.CallId, Text((JsonElement)result.Result!))
            {
                Exception = result.Exception,
                AdditionalProperties = result.AdditionalProperties,
                Annotations = result.Annotations,
                RawRepresentation = result.RawRepresentation
            };
        }

        // The escaping of Microsoft.Extensions.AI's defaults, which a provider's client writes the element with, so
        // only the white space differs
        private static string Text(JsonElement json)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var options = new JsonWriterOptions { Encoder = AIJsonUtilities.DefaultOptions.Encoder };

            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                json.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        private IEnumerable<ChatMessage> AsSent(IEnumerable<ChatMessage> messages) =>
            messages.Any(HoldsJson) ? [.. messages.Select(AsSent)] : messages;

        private ChatMessage AsSent(ChatMessage message)
        {
            if (!HoldsJson(message)) return message;

            var sent = message.Clone();
            sent.Contents = [.. message.Contents.Select(AsSent)];
            return sent;
        }

        private AIContent AsSent(AIContent content) => IsJson(content) ? _sent.GetValue(content, AsText) : content;
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
