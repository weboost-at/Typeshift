#pragma warning disable TYPESHIFT001 // the model policy is experimental, and Native AOT runs it here
using System.Text.Json;
using Typeshift;
using Typeshift.Samples;
using Typeshift.Samples.Chat;
using Typeshift.Samples.Companies;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Runs a toolset, stored settings, a tracked conversation, streaming, structured output, a tool's images, a round of
// decisions, a provider document and a model policy published as Native AOT; any reflection a generated path needed
// would fail here, which fails CI
var services = new ServiceCollection();

// begin-snippet: aot-tracker
services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore<Guid>>()
    .WithConversationTracker<Guid>()
    .WithProviderConnector<ScriptedConnector>();
// end-snippet

// The providers the agents name, under an enum's key, beside the documents the connector turns into clients
// begin-snippet: provider-enum-client
services.AddKeyedChatClient(Provider.Scripted, new ScriptedChatClient());
// end-snippet

// A provider that is down, which a model policy moves the run away from
services.AddKeyedChatClient(DownChatClient.Key, new DownChatClient());

await using var provider = services.BuildServiceProvider();
await using var research = provider.GetRequiredService<ITypeshiftRuntime>().CreateHost<ResearchAgent>();

var conversationId = Guid.CreateVersion7();
var customerId = Guid.CreateVersion7();
var customer = research.WithSettingsId(customerId);

await customer.UpdateSettingsAsync(settings => settings with
{
    Provider = Provider.Scripted,
    Temperature = 0.4f,
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 2 }
});

// The research agent's providers are the enum its WithProvider names, stored and read back without reflection
var stored = await customer.LoadSettingsAsync();
Expect(stored.Data.Provider == Provider.Scripted, $"the provider {stored.Data.Provider}");

// Native AOT: the Guid tracker is the one WithConversationTracker<Guid>() closed; the container makes that of string
var tracker = provider.GetRequiredService<ConversationTracker<Guid>>();
Expect(provider.GetService<ConversationTracker<string>>() is not null, "no tracker of string ids");
var live = tracker.WatchRunsAsync(CancellationToken.None).GetAsyncEnumerator();
var watched = live.MoveNextAsync();

var answer = await customer.WithConversationId(conversationId).RunAsync("Vienna");
Expect(answer.Text == "Found Danube Robotics, Prater Motion.", answer.Text);
Expect(await watched && live.Current.Conversation == conversationId, "no run watched");

var entries = await provider.GetRequiredService<ITypeshiftStore<Guid>>().LoadConversationAsync(conversationId, default);
Expect(entries is [.., { EndsRun: true }], $"{entries.Count} entries without an end");

var streamed = new System.Text.StringBuilder();

await foreach (var update in customer.RunStreamingAsync("Graz"))
{
    streamed.Append(update.Text);
}

Expect(streamed.ToString() == "Found Alpine Grippers.", streamed.ToString());

var count = await customer.RunAsync<CompanyCount>("Vienna");
Expect(count.Result == new CompanyCount("Vienna", 2), count.Text);

var counting = customer.RunStreamingAsync<CompanyCount>("Graz");
await foreach (var _ in counting) { }
Expect(counting.Response.Result == new CompanyCount("Graz", 1), counting.Response.Text);

// An enum travels as its name where JSON does not reflect, since AppJsonContext says so: in the schema the model
// reads, in the argument it sends and in the result it gets back
var offered = await customer.ResolveOptionsAsync();
var countCompanies = offered.Tools!.OfType<AIFunction>()
    .Single(tool => tool.Name == CompanyToolset.ToolNames.CountCompanies);
var sector = countCompanies.JsonSchema.GetProperty("properties").GetProperty("sector");
Expect(sector.TryGetProperty("enum", out _), $"the schema {sector.GetRawText()}");

var robotics = await customer.RunAsync("Robotics");
Expect(robotics.Text == "2 companies work in Robotics.", robotics.Text);

// A tool's images reach the model as images, and the conversation stores them and reads them back as images
var gallery = Guid.CreateVersion7();
var logos = await customer.WithConversationId(gallery).RunAsync("Logos of Graz");
Expect(logos.Text == "1 logos.", logos.Text);

var shown = await provider.GetRequiredService<ITypeshiftStore<Guid>>().LoadConversationAsync(gallery, default);
var images = shown.SelectMany(entry => entry.Messages).SelectMany(message => message.Contents);
Expect(images.Any(content => content is FunctionResultContent { Result: IList<AIContent> }), "the logos as JSON");

// Decisions: questions asked and answered, then a tool call approved, each stored, listed and carried out without
// reflection. The decision and its answer cross JSON as an endpoint would send and receive them
await using var outreach = provider.GetRequiredService<ITypeshiftRuntime>().CreateHost<OutreachAgent>();
var contact = outreach.WithConversationId(Guid.CreateVersion7());

var asking = await contact.RunAsync("Write to a company in Vienna.");
var sent = JsonSerializer.Serialize(asking.Decisions[0], EndpointJsonContext.Default.Decision);
var question = JsonSerializer.Deserialize(sent, EndpointJsonContext.Default.Decision) as QuestionDecision;
Expect(question is { Questions: [{ Header: "Company", Options.Count: 2 }] }, sent);

var posted = $$"""{"decisionId":"{{question!.Id}}","answers":[{"selected":["Prater Motion"]}]}""";
var answers = JsonSerializer.Deserialize(posted, EndpointJsonContext.Default.DecisionAnswer)!;
var answered = await contact.AnswerAsync([answers]);

var open = (await contact.GetDecisionsAsync()).Open;
Expect(open is [ApprovalDecision { Tool: "send_mail" }] && answered.Decisions.Count == 1, $"{open.Count} open");

var mailed = await contact.AnswerAsync([DecisionAnswer.Approve(open[0].Id)]);
Expect(mailed.Text == "Sent to Prater Motion.", mailed.Text);

// The catalog lists the agents and toolsets their modules registered, and creates the host of a kind it lists, as
// an endpoint does that takes the agent from its route: closed over the agent's type at registration, not reflected
var runtime = provider.GetRequiredService<ITypeshiftRuntime>();
var kinds = string.Join(", ", runtime.Agents.Select(agent => agent.Kind).Order(StringComparer.Ordinal));
Expect(kinds == "OutreachAgent, ResearchAgent", kinds);

var tools = runtime.Toolsets.SelectMany(toolset => toolset.Tools).Select(tool => tool.Name).ToList();
Expect(tools.Contains(CompanyToolset.ToolNames.CountCompanies), string.Join(", ", tools));

await using var byKind = runtime.CreateHost("ResearchAgent");
var looked = await byKind.WithSettingsId(customerId).RunAsync("Vienna");
Expect(looked.Text == answer.Text, looked.Text);

// A document of the app's own type, handed to the run at its call site, over the provider its settings store: the
// connector builds its client once, and an equal document's run takes the same client
var tenant = new TenantProvider("tenant-7") { Kind = "scripted", ApiKey = "not-a-key", Model = "scripted-model" };
var onDocument = await customer.WithProvider(tenant).RunAsync("Vienna");
var onEqual = await customer.WithProvider(tenant with { }).RunAsync("Vienna");
Expect(onDocument.Text == answer.Text && onEqual.Text == answer.Text, onDocument.Text);
Expect(!tenant.ToString().Contains("not-a-key", StringComparison.Ordinal), "the document's key in its text");

// A model policy handed to the run: the provider that is down fails as Unavailable, and the run moves to the document
var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToProvider(tenant);
var fallen = await customer.WithProvider(DownChatClient.Key).WithModelPolicy(policy).RunAsync("Vienna");
Expect(fallen.Text == answer.Text, fallen.Text);

Console.WriteLine(
    $"{research.Agent.Name}: {answer.Text} {streamed} {count.Result} {counting.Response.Result} {robotics.Text} "
    + $"{logos.Text}; {entries.Count} entries stored; {outreach.Agent.Name}: {mailed.Text}; {kinds} in the catalog");
return 0;

static void Expect(bool holds, string what)
{
    if (!holds) throw new InvalidOperationException($"Unexpected: {what}");
}
