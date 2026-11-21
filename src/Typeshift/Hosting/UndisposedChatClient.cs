using Microsoft.Extensions.AI;

namespace Typeshift;

// The app owns the clients it registered, and Typeshift keeps a document's client for later runs (D88): disposing a
// run's pipeline disposes neither, and only ends the run's hold on a kept client
internal sealed class UndisposedChatClient(IChatClient inner, IDisposable? hold) : DelegatingChatClient(inner)
{
    protected override void Dispose(bool disposing)
    {
        if (disposing) hold?.Dispose();
    }
}
