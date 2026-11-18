using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Scenarios;

// A guardrail on what a model says wraps the provider's client, so every place an answer goes holds what it left:
// the caller, the stream, the stored conversation and the next model call
internal static class OutputGuardrail
{
    private const string CardNumber = "4111 1111 1111 1111";

    public static void ShowRegistration(IServiceCollection services, IChatClient openAi)
    {
        // begin-snippet: output-guardrail-registration
        services.AddKeyedChatClient("openai", openAi)
            .Use(inner => new RedactedCardNumbers(inner));
        // end-snippet
    }

    public static async Task RunAsync()
    {
        var model = new CardReadingModel();

        await using var runtime = TypeshiftRuntime.Create(
            new RedactedCardNumbers(model),
            typeshift => typeshift.WithStore<InMemoryTypeshiftStore<int>>());

        await using var research = runtime.CreateHost<ResearchAgent>();
        var chat = research.WithConversationId(1);

        var answer = await chat.RunAsync("Which card is on file?");
        var streamed = await chat.RunStreamingAsync("And once more?").ToListAsync();

        var stored = await runtime.Services.GetRequiredService<IConversationStore<int>>()
            .LoadConversationAsync(1, CancellationToken.None);

        string[] held =
        [
            answer.Text,
            .. streamed.Select(update => update.Text),
            .. stored.SelectMany(entry => entry.Messages).Select(message => message.Text),
            .. model.Received
        ];

        if (!answer.Text.Contains("[card number]") || held.Any(text => text.Contains(CardNumber)))
            throw new InvalidOperationException("Expected the card number redacted wherever the answer goes.");
    }

    // Stands in for a model that repeats a card number, and keeps the text of every message it was sent
    private sealed class CardReadingModel : IChatClient
    {
        public List<string> Received { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Received.AddRange(messages.Select(message => message.Text));

            var answer = new ChatMessage(ChatRole.Assistant, $"The card on file is {CardNumber}.");
            return Task.FromResult(new ChatResponse(answer) { ModelId = options?.ModelId });
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
}
