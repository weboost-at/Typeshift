using Bogus;

namespace Typeshift.Tests.Agents.Support;

public class AgentTestData(Faker faker)
{
    private static readonly string[] Models = ["gpt-5-mini", "claude-sonnet-5-5", "mistral-large", "llama-4-scout"];

    // The probe agent's code temperature is 0.2; Default and Updated stay clear of it and of each other
    public TestValue<float> Temperature { get; } =
        new TestValue<float>(MathF.Round(faker.Random.Float(0.3f, 0.9f), 2))
            .SetUpdated(MathF.Round(faker.Random.Float(1.0f, 1.9f), 2));

    public string Instructions { get; } = faker.Lorem.Sentence();

    public Guid ConversationId { get; } = faker.Random.Guid();

    public string Model { get; } = faker.PickRandom(Models);

    // The ids settings are stored under, a tenant each
    public TestValue<Guid> Id { get; } = new TestValue<Guid>(faker.Random.Guid()).SetUpdated(faker.Random.Guid());
}
