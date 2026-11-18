using System.Diagnostics;
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

    // 2,047 B when set
    private const int EntryBudget = 2_500;

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
        await host.RunAsync(Data.Tool.Question, ct: Ct);
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
        new("probe_agent", [new ChatMessage(role, Data.Tool.Question)], Time.GetUtcNow())
        {
            Usage = role == ChatRole.Assistant ? new UsageDetails { InputTokenCount = Data.Agent.InputTokens } : null
        };

    private ConversationEntry End() => new("probe_agent", [], Time.GetUtcNow()) { EndsRun = true };
}
