using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift;
using Typeshift.Demos.Hosting;
using Typeshift.Demos.Providers;
using Typeshift.Demos.Support;
using Typeshift.Demos.Support.Agents;
using Typeshift.Demos.Support.Domain;
using Typeshift.Demos.Support.Hosting;

// A support desk of four agents: triage hands the conversation to billing or support, support asks a help center
// sub-agent and an MCP status server, billing refunds behind the host's approval, every run learns the customer from
// a context provider, a guardrail middleware answers some topics itself, each customer has settings of their own,
// and a report agent summarizes the conversation as typed JSON.
//
//   dotnet run --project demos/Typeshift.Demos.Support -- [provider/model] [--trace]
var catalog = Demo.Catalog();
using var spans = Demo.Flag(args, "--trace") ? new ConsoleSpans() : null;
var model = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));

var canned = new CannedModels
{
    ["triage"] = ScriptedDesk.Triage(),
    ["support"] = ScriptedDesk.Support(),
    ["billing"] = ScriptedDesk.Billing(),
    ["knowledge"] = ScriptedDesk.Knowledge(),
    ["report"] = ScriptedDesk.Report()
};

var services = new ServiceCollection();
services.AddSingleton(TimeProvider.System);
services.AddSingleton<CrmData>();
services.AddScoped<CurrentCustomer>();
services.AddDemoServices(catalog, canned);

services.AddTypeshift()
    .WithChatClientProvider<RoutingChatClientProvider>()
    .WithRealtimeTracking()
    .WithMcpToolProvider<StatusMcpProvider>()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditedCalls>();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

// A request's scope: the customer, then the hosts resolved from it see her everywhere
await using var scope = provider.CreateAsyncScope();
scope.ServiceProvider.GetRequiredService<CurrentCustomer>().Set(CrmData.Ada, "Ada", "Business");

var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
var billing = scope.ServiceProvider.GetRequiredService<AgentHost<BillingAgent>>();
var knowledge = scope.ServiceProvider.GetRequiredService<AgentHost<KnowledgeAgent>>();
var report = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>();

Demo.Heading("Each customer has settings per agent, under one settings id");
var ada = CrmData.Ada;

await triage.ConfigureAsync(ada, settings => settings with { Model = model ?? "canned/triage" });
await billing.ConfigureAsync(ada, settings => settings with { Model = model ?? "canned/billing" });
await knowledge.ConfigureAsync(ada, settings => settings with { Model = model ?? "canned/knowledge" });
await report.ConfigureAsync(ada, settings => settings with { Model = model ?? "canned/report" });

var configured = await support.ConfigureAsync(ada, settings => settings with
{
    Model = model ?? "canned/support",
    Instructions = "Answer in short sentences and address the customer by name.",
    SearchTickets = settings.SearchTickets with { Limit = 3, IncludeClosed = true }
});

Demo.Note($"support: {configured.Serialize()}");

Demo.Heading("Triage hands the conversation to billing, which asks before it refunds");
var conversation = Guid.CreateVersion7();
var first = await triage.RunAsync("Hi, I was charged twice for invoice INV-7.", ada, conversation);
Console.WriteLine($"{first.Messages[^1].AuthorName}> {first.Text}");

foreach (var request in first.ApprovalRequests)
    if (request.ToolCall is FunctionCallContent call)
        Demo.Note($"approval needed: {call.Name}({Demo.Arguments(call)})");

Demo.Heading("The host approves the refund for the rest of the conversation");

if (first.ApprovalRequests.Count > 0)
{
    var approvals = first.ApprovalRequests.Select(request => request.ApproveForConversation());
    var refunded = await triage.RunAsync(new ChatMessage(ChatRole.User, [.. approvals]), ada, conversation);
    Console.WriteLine($"{refunded.Messages[^1].AuthorName}> {refunded.Text}");
}
else
{
    // A small model asks the customer instead of calling issue_refund; then there is nothing to approve
    var confirmed = await triage.RunAsync("Yes, please refund the duplicate charge.", ada, conversation);
    Console.WriteLine($"{confirmed.Messages[^1].AuthorName}> {confirmed.Text}");

    if (confirmed.ApprovalRequests.Count > 0)
    {
        var approvals = confirmed.ApprovalRequests.Select(request => request.ApproveForConversation());
        var refunded = await triage.RunAsync(new ChatMessage(ChatRole.User, [.. approvals]), ada, conversation);
        Console.WriteLine($"{refunded.Messages[^1].AuthorName}> {refunded.Text}");
    }
}

Demo.Heading("A follow-up reaches the agent that answered last, which hands back to support");
var followUp = await triage.RunAsync("Thanks! Also, my ticket about logging in: is there a fix?", ada, conversation);
Console.WriteLine($"{followUp.Messages[^1].AuthorName}> {followUp.Text}");

Demo.Heading("The MCP status tool, offered next to the toolsets");
var status = await support.RunAsync("Is the login service down?", ada);
Console.WriteLine($"{status.Messages[^1].AuthorName}> {status.Text}");

Demo.Heading("Run middleware answers a blocked topic without a model");
var blocked = await support.RunAsync("Can you read me my card number?", ada);
Console.WriteLine($"{blocked.Messages[^1].AuthorName}> {blocked.Text}");

Demo.Heading("The raw path: the options a run resolves, and the tools the support agent offers");
var options = await support.ResolveOptionsAsync(ada);
Demo.Note($"model {options.ModelId}, temperature {options.Temperature}, tools: {string.Join(", ", options.Tools!.Select(tool => tool.Name))}");
Demo.Note($"instructions: {options.Instructions}");

Demo.Heading("A typed summary of the conversation");
var store = provider.GetRequiredService<JsonFileStore>();
var entries = await store.LoadConversationAsync(conversation, CancellationToken.None);
var transcript = string.Join("\n", entries.SelectMany(entry => entry.Messages).Where(message => message.Text.Length > 0)
    .Select(message => $"{message.AuthorName ?? message.Role.Value}: {message.Text}"));

var summary = await report.RunAsync<TicketSummary>($"Summarize this conversation:\n{transcript}", ada);
Demo.Note($"issue: {summary.Result.Issue}");
Demo.Note($"outcome: {summary.Result.Outcome}, refund issued: {summary.Result.RefundIssued}");
Demo.Note($"next: {summary.Result.NextStep}");

Demo.Heading("What the tools changed");
foreach (var line in provider.GetRequiredService<CrmData>().Log) Demo.Note(line);
Demo.Note($"{entries.Count} entries stored for conversation {conversation}, last answered by {entries[^1].Agent}");

return 0;
