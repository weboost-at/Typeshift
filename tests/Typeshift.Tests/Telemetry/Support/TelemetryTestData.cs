using Bogus;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Telemetry.Support;

public class TelemetryTestData(Faker faker)
{
    // Two backends one provider id serves, a customer's deployment each; the customers keep them apart
    public ChatClientMetadata Backend { get; } = BackendOf("acme", faker);

    public ChatClientMetadata SecondBackend { get; } = BackendOf("globex", faker);

    // The models a provider makes a client each for, as OpenAI's GetChatClient(model) does
    public IReadOnlyList<string> Models { get; } =
        [.. Enumerable.Range(1, 3).Select(number => $"{faker.Hacker.Noun()}-model-{number}")];

    private static ChatClientMetadata BackendOf(string customer, Faker faker) =>
        new($"openai-of-{customer}", new Uri($"https://{customer}-{faker.Random.AlphaNumeric(6)}.example"));
}
