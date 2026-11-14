using Bogus;

namespace Typeshift.Tests.Decisions.Support;

public class QuestionTestData(Faker faker)
{
    private static readonly string[] Libraries = ["Dapper", "EF Core", "Marten", "NHibernate", "LINQ to DB"];

    public string Header { get; } = $"{faker.Hacker.Noun()} library";

    public string Text { get; } = $"{faker.Lorem.Sentence().TrimEnd('.')}?";

    // The options of a question: Default and Updated are two of them, Invalid is one it never offers
    public TestValue<string> Label { get; } = CreateLabels(faker);

    public IReadOnlyList<string> Labels => [Label.Default, Label.Updated, Label["third"]];

    public string SecondHeader { get; } = $"{faker.Hacker.Noun()} scope";

    public string SecondText { get; } = $"{faker.Lorem.Sentence().TrimEnd('.')}?";

    public string OwnWords { get; } = faker.Lorem.Sentence();

    private static TestValue<string> CreateLabels(Faker faker)
    {
        var picked = faker.Random.Shuffle(Libraries).ToList();

        return new TestValue<string>(picked[0])
            .SetUpdated(picked[1])
            .Add("third", picked[2])
            .SetInvalid(picked[3]);
    }
}
