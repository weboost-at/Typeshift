using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// An enum, so the Native AOT build covers a provider id type other than string, in code and in stored settings
// begin-snippet: provider-enum
internal enum Provider
{
    Scripted
}

// A real host maps each provider to its client here
internal sealed class ScriptedChatClientProvider : IChatClientProvider<Provider>
{
    private static readonly ScriptedChatClient Client = new();

    public ValueTask<IChatClient> GetChatClientAsync(Provider provider, string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(Client);
}
// end-snippet
