using Bogus;

namespace Typeshift.Tests.Agents.Support;

public class AgentTestData(Faker faker)
{
    public string Model { get; } = $"{faker.Hacker.Noun()}-model";

    // The probe agent's code sets 0.5; a run's temperature stays clear of it
    public float Temperature { get; } = faker.Random.Float(0.6f, 1.5f);

    public string Instructions { get; } = faker.Lorem.Sentence();

    public string CompanyName { get; } = faker.Company.CompanyName();
}
