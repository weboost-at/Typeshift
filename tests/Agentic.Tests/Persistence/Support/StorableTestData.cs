using Bogus;

namespace Agentic.Tests.Persistence.Support;

public class StorableTestData(Faker faker)
{
    public EveryKind EveryKind { get; } = new()
    {
        Text = faker.Lorem.Sentence(),
        Flag = faker.Random.Bool(),
        Count = faker.Random.Int(),
        Big = faker.Random.Long(),
        Ratio = MathF.Round(faker.Random.Float(), 3),
        Precise = Math.Round(faker.Random.Double(), 6),
        Money = Math.Round(faker.Random.Decimal(0m, 10_000m), 2),
        Key = faker.Random.Guid(),
        At = new DateTimeOffset(faker.Date.Recent(), TimeSpan.Zero),
        Span = TimeSpan.FromMinutes(faker.Random.Int(1, 10_000)),
        Shade = faker.PickRandom<Shade>(),
        Tags = [.. faker.Lorem.Words(3)]
    };
}
