namespace Typeshift.Tests.MultiAgent.Support;

// Holds the back agent's first load of its own settings until the gate opens, as a slow store would while a handoff
// passes the conversation on: the agent handed to has not begun its run yet. It keeps no settings
public sealed class GatedBackSettings(BackGate gate) : IAgentSettingsStore
{
    public async Task<StoredDocument?> LoadAsync(string kind, CancellationToken cancellationToken)
    {
        if (kind == nameof(BackAgent) && gate.TakeHold())
        {
            gate.Holding.SetResult();
            await gate.Release.Task.WaitAsync(cancellationToken);

            if (gate.Failure is { } failure) throw failure;
        }

        return null;
    }

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The gated store keeps no settings.");
}

public sealed class BackGate
{
    private int _held;

    public TaskCompletionSource Holding { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? Failure { get; set; }

    public bool TakeHold() => Interlocked.Exchange(ref _held, 1) == 0;
}
