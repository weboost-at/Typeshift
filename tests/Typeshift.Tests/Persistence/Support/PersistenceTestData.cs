using Bogus;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Persistence.Support;

public class PersistenceTestData(Faker faker)
{
    public Guid SettingsId { get; } = faker.Random.Guid();

    // Every member set, none to a default
    public EveryKindAgent.Settings EveryKind { get; } = new()
    {
        Model = $"{faker.Hacker.Noun()}-model",
        Temperature = faker.Random.Float(0.1f, 1.5f),
        StopSequences = [faker.Lorem.Word(), faker.Lorem.Word()],
        Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High, Output = ReasoningOutput.Summary },
        Inspect = new EveryKindToolset.InspectOverrides
        {
            Text = faker.Lorem.Sentence(),
            Flag = true,
            Count = faker.Random.Int(1, 1000),
            Big = faker.Random.Long(1_000_000_000_000),
            Ratio = faker.Random.Float(0.1f, 0.9f),
            Precise = faker.Random.Double(0.1, 0.9),
            Money = faker.Finance.Amount(),
            Key = faker.Random.Guid(),
            When = faker.Date.Past().ToUniversalTime(),
            At = faker.Date.PastOffset(),
            Span = TimeSpan.FromMinutes(faker.Random.Int(1, 10_000)),
            Day = faker.Date.PastDateOnly(),
            Time = faker.Date.RecentTimeOnly(),
            Link = new Uri(faker.Internet.Url()),
            Shade = Shade.Dark,
            Days = Days.Monday | Days.Tuesday,
            Address = new Address { Street = faker.Address.StreetAddress(), City = faker.Address.City() },
            Home = new GeoPoint(faker.Address.Latitude(), faker.Address.Longitude()),
            Tags = [faker.Lorem.Word(), faker.Lorem.Word()],
            Numbers = [faker.Random.Int(), faker.Random.Int()],
            Shades = [Shade.Light, Shade.Dark],
            Scores = new Dictionary<string, int> { [faker.Lorem.Word()] = faker.Random.Int() },
            Places = new Dictionary<Shade, Address> { [Shade.Light] = new() { Street = faker.Address.StreetName() } },
            History = [new Address { City = faker.Address.City() }],
            Route = [new GeoPoint(faker.Address.Latitude(), faker.Address.Longitude())]
        }
    };

    // Every member set, none to a default, and a message of each kind a run stores
    public ConversationEntry Entry { get; } = CreateEntry(faker);

    private static ConversationEntry CreateEntry(Faker faker)
    {
        var callId = faker.Random.AlphaNumeric(12);

        return new(
            faker.Lorem.Word(),
            [
                new ChatMessage(ChatRole.User, faker.Lorem.Sentence()),
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            callId,
                            faker.Lorem.Word(),
                            new Dictionary<string, object?> { ["query"] = faker.Lorem.Word() })
                    ])
                {
                    AuthorName = faker.Lorem.Word()
                },
                new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, faker.Lorem.Sentence())])
            ],
            faker.Date.PastOffset())
        {
            Model = $"{faker.Hacker.Noun()}-model",
            Usage = new UsageDetails
            {
                InputTokenCount = faker.Random.Int(1, 1000),
                OutputTokenCount = faker.Random.Int(1, 1000)
            },
            EndsRun = true,
            Mode = AgentMode.Plan,
            IsSummary = true,
            StandingApprovals = [faker.Lorem.Word()],
            BackgroundCalls = new Dictionary<string, string> { [faker.Random.AlphaNumeric(12)] = faker.Lorem.Word() }
        };
    }
}
