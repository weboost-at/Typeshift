using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Knowledge;

namespace Typeshift.Samples.Scenarios;

// The toolsets the guide shows, called as a model calls them: a union bound by the case its JSON fits, a
// ToolException the model reads as a hint, and the settings of a run that reach a tool
internal static class Toolsets
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var desk = scope.ServiceProvider.GetRequiredService<AgentHost<DeskAgent>>();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        var byNumber = await desk.RunAsync("Find ticket 1234");
        Expect(Results(byNumber).Single().Contains("Refund for order 77"), "a number to bind the union's int case");

        var audited = scope.ServiceProvider.GetRequiredService<SampleLog>().Audited;
        Expect(audited.Contains("Customer 7 calls find_ticket"), "the tool middleware to log the customer's call");

        var byWords = await desk.RunAsync("Find ticket order 77");
        Expect(Results(byWords).Single().Contains("1234"), "words to bind the union's string case");

        var missing = await desk.RunAsync("Escalate ticket 4711");
        var hint = "Error: Function failed. Exception: No ticket 4711 exists. Search for it first.";
        Expect(Results(missing).Single() == hint, "the model to read the ToolException's message");

        var english = await support.RunAsync("Search the help center for Refunds");
        Expect(Results(english).Single().Contains("refunds"), "the search in the settings' language");

        var german = new SupportAgent.Settings
        {
            SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
        };

        var searched = await support.RunAsync("Search the help center for Erstattungen", settings: german);
        Expect(Results(searched).Single().Contains("erstattungen"), "the run's settings to reach the tool");
    }

    private static IEnumerable<string> Results(ChatResponse response) =>
        response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.Result?.ToString() ?? "");

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}
