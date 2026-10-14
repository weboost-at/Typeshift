using Typeshift.Agents;
using Typeshift.Conversations;
using Typeshift.Persistence;
using Typeshift.Persistence.InMemory;
using Typeshift.Samples.Chat;
using Typeshift.Samples.Companies;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Registration: one call registers every toolset and agent of this assembly and of the assemblies it references
// begin-snippet: quickstart-registration
var services = new ServiceCollection();

services.AddTypeshift();
services.AddTypeshiftStore<InMemoryTypeshiftStore<Guid>, Guid>();
services.AddSingleton<IChatClientProvider, ScriptedChatClientProvider>();

await using var provider = services.BuildServiceProvider();
// end-snippet

// Typed settings: ResearchAgent.Settings holds only what the agent's code marks configurable, stored per id
// begin-snippet: quickstart-settings
var research = provider.GetRequiredService<AgentRuntime<ResearchAgent>>();
var customer = Guid.NewGuid();

await research.ConfigureAsync(customer, settings => settings with
{
    Model = "scripted-model",
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});
// end-snippet

// A tool loop: the model calls find_companies, which runs with the settings stored for the customer
// begin-snippet: quickstart-run
var conversationId = Guid.NewGuid();
var options = await research.ResolveChatOptionsAsync(customer, conversationId: conversationId.ToString());
var client = await research.CreateChatClientAsync(options);
var answer = await client.GetResponseAsync("Vienna", options);

Console.WriteLine($"{research.Definition.Name}: {answer.Text}");
// end-snippet

// A tracked conversation: stored under its id through the same store, as JSON
// begin-snippet: quickstart-conversation
var store = provider.GetRequiredService<ITypeshiftStore<Guid>>();

var conversation = await store.LoadAsync<StoredConversation>(conversationId, CancellationToken.None)
    ?? throw new InvalidOperationException("The conversation was not stored.");

Console.WriteLine(
    $"Stored {conversation.AggregatedMessages.Count} messages of {conversation.ExecutionContext.Value}, " +
    $"{conversation.AggregatedUsage.TotalTokenCount} tokens");
// end-snippet

// Run settings: one run overrides configurable values from the code alone, without storing them
// begin-snippet: quickstart-run-settings
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
// end-snippet
