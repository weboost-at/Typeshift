using Typeshift;
using Typeshift.Samples.Chat;
using Typeshift.Samples.Companies;
using Microsoft.Extensions.DependencyInjection;

// Runs a toolset, stored settings, a tracked conversation, streaming and structured output published as Native AOT;
// any reflection a generated path needed would fail here, which fails CI
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>()
    .WithChatClientProvider<ScriptedChatClientProvider>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var research = scope.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>();
var settingsId = Guid.CreateVersion7();
var conversationId = Guid.CreateVersion7();

await research.ConfigureAsync(settingsId, settings => settings with
{
    Temperature = 0.4f,
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 2 }
});

var answer = await research.RunAsync("Vienna", settingsId, conversationId);
Expect(answer.Text == "Found Danube Robotics, Prater Motion.", answer.Text);

var entries = await provider.GetRequiredService<ITypeshiftStore>().LoadConversationAsync(conversationId, default);
Expect(entries is [.., { EndsRun: true }], $"{entries.Count} entries without an end");

var streamed = new System.Text.StringBuilder();

await foreach (var update in research.RunStreamingAsync("Graz", settingsId))
{
    streamed.Append(update.Text);
}

Expect(streamed.ToString() == "Found Alpine Grippers.", streamed.ToString());

var count = await research.RunAsync<CompanyCount>("Vienna", settingsId);
Expect(count.Result == new CompanyCount("Vienna", 2), count.Text);

Console.WriteLine($"{research.Agent.Name}: {answer.Text} {streamed} {count.Result}; {entries.Count} entries stored");
return 0;

static void Expect(bool holds, string what)
{
    if (!holds) throw new InvalidOperationException($"Unexpected: {what}");
}
