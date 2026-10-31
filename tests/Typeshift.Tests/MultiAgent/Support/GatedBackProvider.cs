using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.MultiAgent.Support;

// Holds the first client the back agent asks for until the gate opens, as a slow provider would while a handoff passes
// the conversation on
public sealed class GatedBackProvider(ScriptedChatClient client, ProviderGate gate) : IChatClientProvider
{
    public async ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct)
    {
        if (model == "back-model" && gate.TakeHold())
        {
            gate.Holding.SetResult();
            await gate.Release.Task.WaitAsync(ct);

            if (gate.Failure is { } failure) throw failure;
        }

        return client;
    }
}

public sealed class ProviderGate
{
    private int _held;

    public TaskCompletionSource Holding { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? Failure { get; set; }

    public bool TakeHold() => Interlocked.Exchange(ref _held, 1) == 0;
}
