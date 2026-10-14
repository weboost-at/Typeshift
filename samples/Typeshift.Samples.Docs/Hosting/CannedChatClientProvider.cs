using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// Serves every model from the canned client, so the quickstart runs without a provider
internal sealed class CannedChatClientProvider : IChatClientProvider
{
    private static readonly CannedChatClient Client = new();

    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(Client);
}
