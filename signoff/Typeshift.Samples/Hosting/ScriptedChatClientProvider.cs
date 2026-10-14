using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

internal sealed class ScriptedChatClientProvider : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) => throw null!;
}
