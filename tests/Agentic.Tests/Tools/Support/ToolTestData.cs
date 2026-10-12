using Bogus;

namespace Agentic.Tests.Tools.Support;

public class ToolTestData(Faker faker)
{
    private static readonly string[] Queries =
        ["weboost gmbh", "vienna web agencies", "austrian saas companies", "graz software startups"];

    // None of them is the probe's code default "fast"
    private static readonly string[] Modes = ["thorough", "balanced", "exhaustive"];

    public string Query { get; } = faker.PickRandom(Queries);

    public string Question { get; } = faker.Lorem.Sentence();

    public string Answer { get; } = faker.Lorem.Sentence();

    public string CallId { get; } = $"call_{faker.Random.AlphaNumeric(12)}";

    public string SecondCallId { get; } = $"call_{faker.Random.AlphaNumeric(12)}";

    // The probe's code default is 10; Default and Updated stay clear of it and of each other
    public TestValue<int> Limit { get; } =
        new TestValue<int>(faker.Random.Int(11, 49))
            .SetUpdated(faker.Random.Int(50, 99));

    public string Mode { get; } = faker.PickRandom(Modes);

    public string Text { get; } = string.Join(' ', faker.Lorem.Words(faker.Random.Int(3, 9)));
}
