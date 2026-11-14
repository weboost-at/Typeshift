using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

internal static class Questions
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        // begin-snippet: question-run
        var chat = support.WithConversationId(34);
        var response = await chat.RunAsync("Open a ticket for my dishwasher.");

        var asked = response.Decisions.OfType<QuestionDecision>().Single();

        foreach (var question in asked.Questions)
        {
            var options = string.Join(", ", question.Options.Select(option => option.Label));
            Console.WriteLine($"{question.Header}: {question.Text} ({options})");
        }
        // end-snippet

        if (asked.Questions is not [{ Header: "Problem", MultiSelect: false }, { Header: "Since" }])
            throw new InvalidOperationException("Expected the two questions the model asked.");

        // begin-snippet: question-answer
        var resumed = await chat.AnswerAsync([asked.Answer("It leaks", "Since the last service, two weeks ago.")]);
        // end-snippet

        if (resumed.Text != CannedChatClient.Answer || resumed.Decisions.Count > 0)
            throw new InvalidOperationException("Expected the run to go on with the answers.");

        var again = await chat.RunAsync("Open a ticket for my oven too.");
        var declined = await chat.AnswerAsync([again.Decisions[0].Reject("I will call instead.")]);

        if (declined.Decisions.Count > 0)
            throw new InvalidOperationException("Expected the declined questions to settle.");
    }
}
