using Typeshift;
using Typeshift.Samples.Chat;
using Typeshift.Samples.Companies;
using Microsoft.Extensions.DependencyInjection;

// Runs a toolset, stored settings, a tracked conversation, streaming and structured output published as Native AOT;
// any reflection a generated path needed would fail here, which fails CI
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore<Guid>>()
    .WithChatClientProvider<ScriptedChatClientProvider>();

await using var provider = services.BuildServiceProvider();
await using var research = provider.GetRequiredService<TypeshiftRuntime>().Agent<ResearchAgent>();

var conversationId = Guid.CreateVersion7();
var customer = research.WithSettingsId(Guid.CreateVersion7());

await customer.ConfigureAsync(settings => settings with
{
    Temperature = 0.4f,
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 2 }
});

var answer = await customer.WithConversationId(conversationId).RunAsync("Vienna");
Expect(answer.Text == "Found Danube Robotics, Prater Motion.", answer.Text);

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

Console.WriteLine(
    $"{research.Agent.Name}: {answer.Text} {streamed} {count.Result} {counting.Response.Result}; "
    + $"{entries.Count} entries stored");
return 0;

static void Expect(bool holds, string what)
{
    if (!holds) throw new InvalidOperationException($"Unexpected: {what}");
}
