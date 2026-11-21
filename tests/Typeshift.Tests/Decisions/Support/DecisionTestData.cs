using System.Text.Json;
using Bogus;

namespace Typeshift.Tests.Decisions.Support;

public class DecisionTestData(Faker faker)
{
    public string DecisionId { get; } = faker.Random.Guid().ToString("N");

    public string Plan { get; } = faker.Lorem.Paragraph();

    public string Server { get; } = $"{faker.Hacker.Noun()}-server";

    // Every member set, none to a default: a mode, a standing approval of a server's tool, a batch with a call of
    // each kind and an answer of each kind, the last one questions, and a closed decision
    internal DecisionDocument Document { get; } = CreateDocument(faker);

    // Two questions as ask_user takes them, the first with several options to pick
    private static readonly JsonElement Questions = JsonElement.Parse(
        """
        {
          "questions": [
            {
              "question": "Which libraries may the data layer use?",
              "header": "Libraries",
              "options": [
                { "label": "Dapper", "description": "Plain SQL." },
                { "label": "EF Core", "description": "An object mapper." }
              ],
              "multiSelect": true
            },
            {
              "question": "How far should the change reach?",
              "header": "Scope",
              "options": [
                { "label": "Everything", "description": "Every table." },
                { "label": "A part", "description": "The tables you name." }
              ]
            }
          ]
        }
        """);

    private static DecisionDocument CreateDocument(Faker faker)
    {
        var asked = faker.Date.PastOffset().ToUniversalTime();
        var arguments = JsonElement.Parse($$"""{"number":{{faker.Random.Int(1, 9999)}}}""");

        BatchCall Call(string tool) => new($"call_{faker.Random.AlphaNumeric(12)}", tool, arguments);

        Ask Answered(AskKind kind, Func<string, DecisionAnswer> answer)
        {
            var id = faker.Random.Guid().ToString("N");
            return new Ask(id, kind) { Answer = answer(id) };
        }

        return new DecisionDocument
        {
            Mode = AgentMode.Plan,
            Standing = [new StandingApproval(faker.Hacker.Verb(), $"{faker.Hacker.Noun()}-server", asked)],
            Batch = new DecisionBatch(
                faker.Lorem.Word(),
                asked,
                BatchState.Answered,
                [
                    Call(faker.Hacker.Verb()) with
                    {
                        Server = $"{faker.Hacker.Noun()}-server",
                        Ask = Answered(AskKind.Approval, DecisionAnswer.ApproveForConversation)
                    },
                    Call("exit_plan_mode") with
                    {
                        Ask = Answered(AskKind.Plan, id => DecisionAnswer.ApprovePlan(id, AgentMode.Auto))
                    },
                    Call(faker.Hacker.Verb()) with
                    {
                        Ask = Answered(AskKind.Approval, id => DecisionAnswer.Reject(id, faker.Lorem.Sentence()))
                    },
                    Call(faker.Hacker.Verb()) with { Rejected = faker.Lorem.Sentence() },
                    Call(faker.Hacker.Verb()),
                    Call("ask_user") with
                    {
                        Arguments = Questions,
                        Ask = Answered(
                            AskKind.Question,
                            id => DecisionAnswer.Answer(id, new QuestionAnswer(["Dapper"]), faker.Lorem.Sentence()))
                    }
                ]),
            Closed = [new ClosedDecision(faker.Random.Guid().ToString("N"), DecisionClosedReason.Expired)]
        };
    }
}
