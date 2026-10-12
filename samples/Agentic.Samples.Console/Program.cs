using System.Text.Json;
using Agentic.Agents;
using Agentic.Catalogs;
using Agentic.Conversations;
using Agentic.Json;
using Agentic.Persistence;
using Agentic.Persistence.InMemory;
using Agentic.Samples;
using Agentic.Samples.Chat;
using Agentic.Samples.Companies;
using Agentic.StoredAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Registration: one call registers every toolset and agent of this assembly and of the assemblies it references
var services = new ServiceCollection();

services.AddAgentic();
services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(AppJsonContext.Default));

services.AddSingleton<IChatClientProvider, ScriptedChatClientProvider>();
services.AddSingleton<IAgentConfigurationStore, InMemoryAgentConfigurationStore>();
services.AddSingleton<IToolDefaultsStore, InMemoryToolDefaultsStore>();
services.AddSingleton<IAgentDefinitionStore, InMemoryAgentDefinitionStore>();
services.AddSingleton<IConversationStore, InMemoryConversationStore>();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

var time = provider.GetRequiredService<TimeProvider>();
var conversations = provider.GetRequiredService<IConversationStore>();
var json = provider.GetRequiredService<AgentJson>();

// Typed settings: only what the agent's code marks configurable can be stored
var research = provider.GetRequiredService<AgentRuntime<ResearchAgent>>();

await research.ConfigureAsync(configuration =>
{
    configuration.Model = "scripted-model";
    configuration.Temperature = 0.4f;
    configuration.FindCompanies.Limit = 2;
});

// A tool loop: the model calls find_companies, which runs with the resolved settings
var conversationId = Guid.CreateVersion7(time.GetUtcNow());

var run = new RunOverrides
{
    ChatOptions = new ChatOptions { ConversationId = conversationId.ToString() }
};

var client = await research.CreateChatClientAsync();
var options = await research.ResolveChatOptionsAsync(run);
var answer = await client.GetResponseAsync("Vienna", options);

Console.WriteLine($"{ResearchAgent.Name}: {answer.Text}");

// A tracked conversation: every exchange is stored under the agent that ran it, as a document a store can persist
var conversation = await conversations.LoadAsync(conversationId, CancellationToken.None)
    ?? throw new InvalidOperationException("The conversation was not stored.");

var document = JsonSerializer.Serialize(conversation, json.GetTypeInfo<StoredConversation>());
var restored = JsonSerializer.Deserialize(document, json.GetTypeInfo<StoredConversation>())!;

Console.WriteLine(
    $"Stored {restored.AggregatedMessages.Count} messages of {restored.ExecutionContext.Value}, " +
    $"{restored.AggregatedUsage.TotalTokenCount} tokens, read back from JSON");

// A stored agent: defined as data, it runs through the same path as a code agent
var catalog = provider.GetRequiredService<AgentCatalog>();

var scout = StoredAgent.Create("Market scout", time) with
{
    Description = "Scouts the active companies of a city.",
    Tools = [CompanyToolset.Tools.FindCompanies.Name]
};

await catalog.SaveAsync(scout);

var scoutRuntime = await catalog.GetRuntimeAsync(scout.UniqueName);
await scoutRuntime.UpdateConfigurationAsync(configuration => configuration with { Model = "scripted-model" });

var scoutClient = await scoutRuntime.CreateChatClientAsync();
var scoutOptions = await scoutRuntime.ResolveChatOptionsAsync();

Console.Write($"{scout.DisplayName}: ");

await foreach (var update in scoutClient.GetStreamingResponseAsync("Graz", scoutOptions))
    Console.Write(update.Text);

Console.WriteLine();
