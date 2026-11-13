using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Decisions.Support;

// A ledger over a store in memory whose saves can lose a race, and a second ledger straight on the same store, as
// another process of the app would have
public abstract class LedgerTestBase : HostTestBase
{
    private readonly Lazy<ServiceProvider> _otherProcess;

    protected LedgerTestBase() => _otherProcess = new(BuildOtherProcess);

    protected UnreliableStore Store { get; } = new();

    internal StoreId Conversation => new StoreId<Guid>(Data.Agent.ConversationId);

    internal DecisionLedger Ledger => new(Services.GetRequiredService<StoreAccess>(), Time);

    internal DecisionLedger OtherProcess => new(_otherProcess.Value.GetRequiredService<StoreAccess>(), Time);

    internal Task<DecisionDocument> DocumentAsync() => Ledger.LoadAsync(Conversation, Ct);

    // A batch as the gate opens it: every call of one model response
    internal DecisionBatch Batch(params BatchCall[] calls) =>
        new("guarded_agent", Time.GetUtcNow(), BatchState.Open, calls);

    internal BatchCall Approval(string callId) =>
        new(callId, "close_ticket", Arguments($$"""{"number":{{Data.Agent.Ticket.Default}}}"""))
        {
            Ask = Ask.Create(AskKind.Approval, Time)
        };

    internal BatchCall Plan(string callId) =>
        new(callId, "exit_plan_mode", Arguments($$"""{"plan":{{JsonSerializer.Serialize(Data.Tool.Answer)}}}"""))
        {
            Ask = Ask.Create(AskKind.Plan, Time)
        };

    internal BatchCall Sibling(string callId) => new(callId, "note", Arguments("""{"text":"noted"}"""));

    // A batch whose every decision is approved, as the last answer leaves it
    internal async Task<DecisionBatch> AnsweredAsync(DecisionBatch batch)
    {
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var answered = await Ledger.RecordAsync(Conversation, [.. batch.Ids.Select(DecisionAnswer.Approve)], Ct);

        return answered.Batch!;
    }

    // A batch a run began to carry out
    internal async Task<DecisionBatch> StartedAsync(DecisionBatch batch) =>
        (await Ledger.StartAsync(Conversation, await AnsweredAsync(batch), Ct)).Batch!;

    protected override void ConfigureServices(IServiceCollection services) => services.AddTypeshift().WithStore(Store);

    protected override async ValueTask DisposeCoreAsync()
    {
        if (_otherProcess.IsValueCreated) await _otherProcess.Value.DisposeAsync();
    }

    private ServiceProvider BuildOtherProcess()
    {
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(Store.Inner);

        return services.BuildServiceProvider();
    }

    private static JsonElement Arguments(string json) => JsonElement.Parse(json);
}
