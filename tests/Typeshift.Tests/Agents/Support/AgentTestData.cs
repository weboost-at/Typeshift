using Bogus;

namespace Typeshift.Tests.Agents.Support;

public class AgentTestData(Faker faker)
{
    public string Provider { get; } = $"{faker.Hacker.Noun()}-provider";

    public string Model { get; } = $"{faker.Hacker.Noun()}-model";

    // The probe agent's code sets 0.5; a run's temperature stays clear of it
    public float Temperature { get; } = faker.Random.Float(0.6f, 1.5f);

    public string Instructions { get; } = faker.Lorem.Sentence();

    public string CompanyName { get; } = faker.Company.CompanyName();

    public Guid ConversationId { get; } = faker.Random.Guid();

    public string FollowUp { get; } = faker.Lorem.Sentence();

    public string Steering { get; } = faker.Lorem.Sentence();

    public TestValue<int> Ticket { get; } =
        new TestValue<int>(faker.Random.Int(1, 4999)).SetUpdated(faker.Random.Int(5000, 9999));

    public string Reason { get; } = faker.Lorem.Sentence();

    public long InputTokens { get; } = faker.Random.Long(100, 100_000);

    public long OutputTokens { get; } = faker.Random.Long(1, 99);
}
