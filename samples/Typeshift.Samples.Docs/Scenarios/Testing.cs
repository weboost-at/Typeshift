using Microsoft.Extensions.AI;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Scenarios;

// A test of the quick start's agent on a scripted model, as the guide shows it: the model calls the agent's tool, the
// tool runs, and the test reads what the model was sent and what the run answered. Then the tool alone, called as a
// method
internal static class Testing
{
    private const string Answer = "Found three companies in Vienna.";

    public static async Task RunAsync()
    {
        // begin-snippet: testing-an-agent
        var arguments = new Dictionary<string, object?> { ["city"] = "Vienna" };

        var model = new ScriptedChatClient(
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_1", "find_companies", arguments)]),
            new ChatMessage(ChatRole.Assistant, "Found three companies in Vienna."));

        await using var runtime = TypeshiftRuntime.Create(model);
        await using var research = runtime.CreateHost<ResearchAgent>();

        var answer = await research.RunAsync("Which companies are in Vienna?");

        var sent = model.Calls[0].Options!;

        var result = model.Calls[1].Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        // end-snippet

        Expect(answer.Text == Answer, "the run to return the scripted answer");
        Expect(sent.ModelId == "gpt-5-mini", "the model to be sent the agent's model");
        Expect(sent.Instructions?.StartsWith("Find companies") is true, "the agent's instructions to be sent");
        Expect(sent.Tools?.Any(tool => tool.Name == "find_companies") is true, "the model to be offered the tool");
        Expect(result.Result is string json && json.Contains("Belvedere Optics"), "the model to read the result");

        var found = CompanyToolset.FindCompanies("Vienna", new FindCompaniesSettings { Limit = 2 });
        Expect(found.Count == 2, "the tool, called as a method, to keep to its limit");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}
