using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// D88: connects each document to a client of its own over the test's scripted client, and keeps every client it
// connected, in order. A test that holds the gate closed holds each connection until it opens
public sealed class CountingConnector(ScriptedChatClient client, Connections connections) : IChatProviderConnector
{
    public async ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken cancellationToken)
    {
        var connected = new ConnectedClient(client, provider);
        connections.Add(connected);

        await connections.Gate.Task.WaitAsync(cancellationToken);
        return connected;
    }
}

public sealed class Connections : List<ConnectedClient>
{
    // Open unless a test closes it, with a gate of its own in its place
    public TaskCompletionSource Gate { get; set; } = Opened();

    private static TaskCompletionSource Opened()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }
}

// A client Typeshift keeps for a document, which knows whether it was disposed; it leaves the scripted client alone
public sealed class ConnectedClient(IChatClient inner, ChatProvider provider) : DelegatingChatClient(inner)
{
    public ChatProvider Provider => provider;

    public bool Disposed { get; private set; }

    protected override void Dispose(bool disposing) => Disposed = true;
}

// A connector that cannot connect, as one whose vault refuses the document's key
public sealed class FailingConnector : IChatProviderConnector
{
    public const string Reason = "The vault refused the key.";

    public ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Reason);
}

// A provider document of the app's own, with a member the base record lacks
public sealed record CustomerProvider(Guid Customer) : ChatProvider;
