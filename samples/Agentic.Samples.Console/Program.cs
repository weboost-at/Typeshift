using Agentic.Agents;
using Agentic.Conversations;
using Agentic.Json;
using Agentic.Persistence;
using Agentic.Persistence.InMemory;
using Agentic.Samples;
using Agentic.Samples.Chat;
using Agentic.Samples.Companies;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Registration: one call registers every toolset and agent of this assembly and of the assemblies it references
var services = new ServiceCollection();

services.AddAgentic();
services.AddAgenticStore<InMemoryAgenticStore<Guid>, Guid>();
services.AddSingleton<IChatClientProvider, ScriptedChatClientProvider>();
services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(AppJsonContext.Default));

await using var provider = services.BuildServiceProvider();

var time = provider.GetRequiredService<TimeProvider>();
var research = provider.GetRequiredService<AgentRuntime<ResearchAgent>>();

// Typed settings: ResearchAgent.Settings holds only what the agent's code marks configurable, stored per id
var customer = Guid.CreateVersion7(time.GetUtcNow());

await research.ConfigureAsync(customer, settings => settings with
{
    Model = "scripted-model",
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});

// A tool loop: the model calls find_companies, which runs with the settings stored for the customer
var conversationId = Guid.CreateVersion7(time.GetUtcNow());
var options = await research.ResolveChatOptionsAsync(customer, conversationId: conversationId.ToString());
var client = await research.CreateChatClientAsync(options);
var answer = await client.GetResponseAsync("Vienna", options);

Console.WriteLine($"{research.Definition.Name}: {answer.Text}");

// A tracked conversation: stored under its id through the same store, as JSON
var store = provider.GetRequiredService<IAgenticStore<Guid>>();

var conversation = await store.LoadAsync<StoredConversation>(conversationId, CancellationToken.None)
    ?? throw new InvalidOperationException("The conversation was not stored.");

Console.WriteLine(
    $"Stored {conversation.AggregatedMessages.Count} messages of {conversation.ExecutionContext.Value}, " +
    $"{conversation.AggregatedUsage.TotalTokenCount} tokens");

// Run settings: one run overrides configurable values from the code alone, without storing them
var run = new ResearchAgent.Settings
{
    Model = "scripted-model",
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
};

var runOptions = research.ResolveChatOptions(run);
var runClient = await research.CreateChatClientAsync(runOptions);

Console.Write($"{research.Definition.Name}, one run: ");

await foreach (var update in runClient.GetStreamingResponseAsync("Vienna", runOptions))
    Console.Write(update.Text);

Console.WriteLine();
