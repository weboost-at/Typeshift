using System.Diagnostics;
using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Testing;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// Budgets in allocated bytes, each about half again what the code allocated when it was set, so a change that
// doubles a cost fails here. A change that needs more raises the budget in the same commit, where it is reviewed
[Collection(Allocations.Collection)]
public class AllocationBudgetTests : AgentTestBase
{
    // 18,424 B when set
    private const int RunBudget = 28_000;

    // 12,296 B when set, 14,232 B since each JSON result goes to the provider as text
    private const int ModelCallBudget = 18_000;

    // 2,047 B when set, 1,303 B since the history is built without a list for each of its messages
    private const int EntryBudget = 1_950;

    // An estimate of a long history once the run's first wrote out its JSON: no text of a call, a result or a schema
    // again, where each estimate allocated 7.9 MB before
    private const int EstimateBudget = 64 * 1024;

    private const int Rounds = 4;

    private const int Entries = 900;

    // Spans are recorded, as in an app that exports them, so the budgets hold whatever test attached a listener before
    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = source => source.Name == "Typeshift",
        Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
    };

    public AllocationBudgetTests() => ActivitySource.AddActivityListener(_listener);

    [Fact]
    public async Task Given_No_Tool_Call_A_Run_Allocates_Within_Its_Budget()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        await WarmUpAsync(() => RunAsync(host, rounds: 0));

        // Act
        var allocated = await Allocations.OfAsync(() => RunAsync(host, rounds: 0));

        // Assert
        allocated.Should().BeLessThan(RunBudget);
    }

    [Fact]
    public async Task Given_Tool_Calls_Each_Further_Model_Call_Allocates_Within_Its_Budget()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        await WarmUpAsync(() => RunAsync(host, Rounds));
        var plain = await Allocations.OfAsync(() => RunAsync(host, rounds: 0));

        // Act
        var allocated = await Allocations.OfAsync(() => RunAsync(host, Rounds));

        // Assert
        ((allocated - plain) / Rounds).Should().BeLessThan(ModelCallBudget);
    }

    [Fact]
    public async Task Given_A_Long_Conversation_A_Run_Allocates_Within_Its_Budget_For_Each_Entry()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();

        for (var turn = 0; turn < Entries / 3; turn++)
        {
            await store.AppendConversationAsync(Data.Agent.ConversationId, Entry(ChatRole.User), Ct);
            await store.AppendConversationAsync(Data.Agent.ConversationId, Entry(ChatRole.Assistant), Ct);
            await store.AppendConversationAsync(Data.Agent.ConversationId, End(), Ct);
        }

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await WarmUpAsync(() => RunAsync(host, rounds: 0));

        // Act
        var allocated = await Allocations.OfAsync(() => RunAsync(host, rounds: 0));

        // Assert
        (allocated / Entries).Should().BeLessThan(EntryBudget);
    }

    [Fact]
    public async Task Given_Nobody_Watching_A_Streamed_Answer_Allocates_In_Step_With_Its_Updates()
    {
        // Arrange
        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await WarmUpAsync(() => StreamAsync(host, updates: 100));
        var few = await Allocations.OfAsync(() => StreamAsync(host, updates: 500));

        // Act
        var many = await Allocations.OfAsync(() => StreamAsync(host, updates: 2_000));

        // Assert
        many.Should().BeLessThan(6 * few);
    }

    [Fact]
    public async Task Given_A_Long_History_A_Runs_Later_Estimate_Allocates_Within_Its_Budget()
    {
        // Arrange
        var (messages, options) = LongHistory();
        var lengths = Tokens.Lengths();
        Tokens.Estimate(messages, options, lengths);

        // Act
        var allocated = await Allocations.OfAsync(() =>
        {
            Tokens.Estimate(messages, options, lengths);
            return Task.CompletedTask;
        });

        // Assert
        allocated.Should().BeLessThan(EstimateBudget);
    }

    [Fact]
    public async Task Given_A_Summary_The_Memory_Store_Loads_From_It_Without_Reading_What_Came_Before()
    {
        // Arrange
        var shortStore = await SummarizedAsync(before: 10);
        var longStore = await SummarizedAsync(before: Entries);
        await shortStore.LoadConversationSinceCompactionAsync(Data.Agent.ConversationId, Ct);
        await longStore.LoadConversationSinceCompactionAsync(Data.Agent.ConversationId, Ct);

        var fromShort = await Allocations.OfAsync(() =>
            shortStore.LoadConversationSinceCompactionAsync(Data.Agent.ConversationId, Ct));

        // Act
        var fromLong = await Allocations.OfAsync(() =>
            longStore.LoadConversationSinceCompactionAsync(Data.Agent.ConversationId, Ct));

        // Assert
        fromLong.Should().BeLessThan(2 * fromShort);
    }

    protected override ValueTask DisposeCoreAsync()
    {
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RunAsync(AgentHost<ProbeAgent> host, int rounds)
    {
        for (var round = 0; round < rounds; round++)
        {
            ChatClient.Reply(Call($"{Data.Tool.CallId}-{round}", "probe", ("query", Data.Tool.Query)));
        }

        ChatClient.Reply(Text(Data.Tool.Answer));
        await host.RunAsync(Data.Tool.Question, cancellationToken: Ct);
    }

    // The clock stands still, so every update falls into one publish interval
    private async Task StreamAsync(AgentHost<ProbeAgent> host, int updates)
    {
        ChatClient.Reply([.. Enumerable.Range(0, updates).Select(_ => Text("word "))]);

        await foreach (var _ in host.RunStreamingAsync(Data.Tool.Question, cancellationToken: Ct))
        {
        }
    }

    // A conversation in memory with a summary after the entries before it and three entries after it
    private async Task<InMemoryTypeshiftStore<Guid>> SummarizedAsync(int before)
    {
        var store = new InMemoryTypeshiftStore<Guid>();

        for (var entry = 0; entry < before + 4; entry++)
        {
            var stored = Entry(ChatRole.User) with { IsSummary = entry == before };
            await store.AppendConversationAsync(Data.Agent.ConversationId, stored, Ct);
        }

        return store;
    }

    // A thousand calls, each with its JSON result, and forty tools with a schema each
    private (List<ChatMessage> Messages, ChatOptions Options) LongHistory()
    {
        var payload = JsonElement.Parse($$"""{ "text": "{{new string('x', 2_000)}}" }""");
        var schema = JsonElement.Parse($$"""{ "type": "object", "description": "{{new string('d', 1_000)}}" }""");
        List<ChatMessage> messages = [new(ChatRole.User, Data.Tool.Question)];

        for (var call = 0; call < 1_000; call++)
        {
            var id = $"{Data.Tool.CallId}_{call}";
            var arguments = new Dictionary<string, object?> { ["query"] = payload };

            messages.Add(new(ChatRole.Assistant, [new FunctionCallContent(id, "read", arguments)]));
            messages.Add(new(ChatRole.Tool, [new FunctionResultContent(id, payload)]));
        }

        var tools = Enumerable.Range(0, 40)
            .Select(tool => AIFunctionFactory.CreateDeclaration($"tool_{tool}", "", schema));
        var options = new ChatOptions { Tools = [.. tools] };

        return (messages, options);
    }

    // Static state and caches fill on the first passes
    private static async Task WarmUpAsync(Func<Task> run)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            await run();
        }
    }

    // An answer carries the tokens its provider reported, as every stored answer of a real provider does
    private ConversationEntry Entry(ChatRole role) =>
        new()
        {
            Agent = "probe_agent",
            Messages = [new ChatMessage(role, Data.Tool.Question)],
            At = Time.GetUtcNow(),
            Usage = role == ChatRole.Assistant ? new UsageDetails { InputTokenCount = Data.Agent.InputTokens } : null
        };

    private ConversationEntry End() => new() { Agent = "probe_agent", At = Time.GetUtcNow(), EndsRun = true };
}
