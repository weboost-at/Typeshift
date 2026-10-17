using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// The providers this host knows, its TypeshiftProviderIdType
internal enum Provider
{
    Scripted
}

// A real host maps each provider to its client here
internal sealed class ScriptedChatClientProvider : IChatClientProvider
{
    private static readonly ScriptedChatClient Client = new();

    public ValueTask<IChatClient> GetChatClientAsync(Provider provider, string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(Client);
}
