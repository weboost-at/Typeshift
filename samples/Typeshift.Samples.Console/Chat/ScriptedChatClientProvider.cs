using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// A real host maps each model to a provider client here
internal sealed class ScriptedChatClientProvider : IChatClientProvider
{
    private static readonly ScriptedChatClient Client = new();

    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(Client);
}
