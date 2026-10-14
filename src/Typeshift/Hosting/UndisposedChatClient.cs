using Microsoft.Extensions.AI;

namespace Typeshift;

// The provider owns its clients; disposing a run's pipeline must not dispose them
internal sealed class UndisposedChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    protected override void Dispose(bool disposing)
    {
    }
}
