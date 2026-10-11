using Bogus;

namespace Agentic.Tests.Agents.Support;

public class AgentTestData(Faker faker)
{
    private static readonly string[] Models = ["gpt-5-mini", "claude-sonnet-5-5", "mistral-large", "llama-4-scout"];

    private static readonly string[] DisplayNames = ["Lead qualifier", "Inbox triage", "Market scout", "Deal desk"];

    private static readonly string[] Contexts = ["ProfileCompanyInformation", "SummarizeInbox", "DraftOutreach"];

    // The probe agent's code temperature is 0.2; Default and Updated stay clear of it and of each other
    public TestValue<float> Temperature { get; } =
        new TestValue<float>(MathF.Round(faker.Random.Float(0.3f, 0.9f), 2))
            .SetUpdated(MathF.Round(faker.Random.Float(1.0f, 1.9f), 2));

    public int MaxOutputTokens { get; } = faker.Random.Int(256, 4096);

    public string Instructions { get; } = faker.Lorem.Sentence();

    public string ConversationId { get; } = faker.Random.Guid().ToString();

    public string OtherContext { get; } = faker.PickRandom(Contexts);

    public string Model { get; } = faker.PickRandom(Models);

    public string DisplayName { get; } = faker.PickRandom(DisplayNames);

    public string UnknownTool { get; } = $"retired_tool_{faker.Random.AlphaNumeric(6).ToLowerInvariant()}";
}
