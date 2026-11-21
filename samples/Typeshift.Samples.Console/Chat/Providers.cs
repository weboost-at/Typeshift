using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// An enum, so the Native AOT build covers a provider id type other than string, in code and in stored settings
// begin-snippet: provider-enum
internal enum Provider
{
    Scripted
}
// end-snippet

// A provider document of the app's own, so the Native AOT build covers a derived record taken as a document
internal sealed record TenantProvider(string Tenant) : ChatProvider;

// A real app builds the client of each kind from the document's key and endpoint here
internal sealed class ScriptedConnector : IChatProviderConnector
{
    public ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken ct) =>
        provider.Kind == "scripted"
            ? ValueTask.FromResult<IChatClient>(new ScriptedChatClient())
            : throw new InvalidOperationException($"No client for the provider kind '{provider.Kind}'.");
}
