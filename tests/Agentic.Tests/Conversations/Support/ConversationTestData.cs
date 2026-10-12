using Bogus;

namespace Agentic.Tests.Conversations.Support;

public class ConversationTestData(Faker faker)
{
    private static readonly string[] Models = ["gpt-5-mini", "claude-haiku-5-5", "mistral-small"];

    public string Model { get; } = faker.PickRandom(Models);

    public string Context { get; } = $"conversation_{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";

    public Guid ConversationId { get; } = faker.Random.Guid();

    public string Question { get; } = faker.Lorem.Sentence();

    public string FollowUp { get; } = faker.Lorem.Sentence();

    public string Answer { get; } = faker.Lorem.Sentences(3);

    public string CallId { get; } = $"call_{faker.Random.AlphaNumeric(12)}";

    public string ResponseId { get; } = $"resp_{faker.Random.AlphaNumeric(12)}";

    public string Failure { get; } = faker.Lorem.Sentence();

    public int InputTokens { get; } = faker.Random.Int(100, 999);

    public int OutputTokens { get; } = faker.Random.Int(10, 99);

    public IReadOnlyList<string> AnswerDeltas => [.. Answer.Chunk(4).Select(characters => new string(characters))];
}
