using System.Text.Json;
using Typeshift;
using Typeshift.Samples;
using Typeshift.Samples.Chat;
using Typeshift.Samples.Companies;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Runs a toolset, stored settings, a tracked conversation, streaming, structured output and a round of decisions
// published as Native AOT; any reflection a generated path needed would fail here, which fails CI
var services = new ServiceCollection();

// begin-snippet: aot-tracker
services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore<Guid>>()
    .WithConversationTracker<Guid>()
    .WithChatClientProvider<ScriptedChatClientProvider>();
// end-snippet

await using var provider = services.BuildServiceProvider();
await using var research = provider.GetRequiredService<TypeshiftRuntime>().Agent<ResearchAgent>();

var conversationId = Guid.CreateVersion7();
var customer = research.WithSettingsId(Guid.CreateVersion7());

await customer.ConfigureAsync(settings => settings with
{
    Provider = Provider.Scripted,
    Temperature = 0.4f,
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 2 }
});

// The research agent's providers are the enum its WithModel names, stored and read back without reflection
var stored = await customer.LoadSettingsAsync();
Expect(stored.Data.Provider == Provider.Scripted, $"the provider {stored.Data.Provider}");

// Native AOT: the Guid tracker is the one WithConversationTracker<Guid>() closed; the container makes that of string
var tracker = provider.GetRequiredService<ConversationTracker<Guid>>();
Expect(provider.GetRequiredService<ConversationTracker<string>>().Runs.Count == 0, "a run under a string id");
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
var countCompanies = offered.Tools!.OfType<AIFunction>().Single(tool => tool.Name == "count_companies");
var sector = countCompanies.JsonSchema.GetProperty("properties").GetProperty("sector");
Expect(sector.TryGetProperty("enum", out _), $"the schema {sector.GetRawText()}");

var robotics = await customer.RunAsync("Robotics");
Expect(robotics.Text == "2 companies work in Robotics.", robotics.Text);

// Decisions: questions asked and answered, then a tool call approved, each stored, listed and carried out without
// reflection. The decision and its answer cross JSON as an endpoint would send and receive them
await using var outreach = provider.GetRequiredService<TypeshiftRuntime>().Agent<OutreachAgent>();
var contact = outreach.WithConversationId(Guid.CreateVersion7());

var asking = await contact.RunAsync("Write to a company in Vienna.");
var shown = JsonSerializer.Serialize(asking.Decisions[0], EndpointJsonContext.Default.Decision);
var question = JsonSerializer.Deserialize(shown, EndpointJsonContext.Default.Decision) as QuestionDecision;
Expect(question is { Questions: [{ Header: "Company", Options.Count: 2 }] }, shown);

var posted = $$"""{"decisionId":"{{question!.Id}}","answers":[{"selected":["Prater Motion"]}]}""";
var answers = JsonSerializer.Deserialize(posted, EndpointJsonContext.Default.DecisionAnswer)!;
var answered = await contact.AnswerAsync([answers]);

var open = (await contact.GetDecisionsAsync()).Open;
Expect(open is [ApprovalDecision { Tool: "send_mail" }] && answered.Decisions.Count == 1, $"{open.Count} open");

var mailed = await contact.AnswerAsync([DecisionAnswer.Approve(open[0].Id)]);
Expect(mailed.Text == "Sent to Prater Motion.", mailed.Text);

Console.WriteLine(
    $"{research.Agent.Name}: {answer.Text} {streamed} {count.Result} {counting.Response.Result} {robotics.Text}; "
    + $"{entries.Count} entries stored; {outreach.Agent.Name}: {mailed.Text}");
return 0;

static void Expect(bool holds, string what)
{
    if (!holds) throw new InvalidOperationException($"Unexpected: {what}");
}
