using Agentic.Agents;
using Agentic.Samples.Docs.Agents;
using Agentic.Samples.Docs.Knowledge;
using Agentic.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Scenarios;

internal static class ToolDefaultsScenario
{
    public static async Task RunAsync()
    {
        await using var services = Host.Create();
        var customer = Guid.NewGuid();

        // begin-snippet: tool-defaults
        var defaults = services.GetRequiredService<ToolDefaults>();

        await defaults.ConfigureAsync(customer, KnowledgeToolset.Tools.SearchArticles, articles => articles with
        {
            Language = "de"
        });
        // end-snippet

        var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();
        var options = await support.ResolveChatOptionsAsync(customer);
        var search = options.Tools!.OfType<AIFunction>().Single(tool => tool.Name == "search_articles");

        var result = await search.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
        {
            ["question"] = "refund"
        })
        {
            Services = services
        });

        Expect.That(result!.ToString()!.Contains("\"de\"", StringComparison.Ordinal), "tool defaults apply");
    }
}
