using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// Serves the vendors' models from the test's scripted client and records them, as ScriptedChatClientProvider does for
// string providers
public sealed class VendorChatClientProvider(ScriptedChatClient client, ProvidedModels models)
    : IChatClientProvider<Vendor>
{
    public ValueTask<IChatClient> GetChatClientAsync(Vendor provider, string model, CancellationToken ct)
    {
        models.Add(new(provider, model));
        return ValueTask.FromResult<IChatClient>(client);
    }
}

// Serves providers of both types, so one registration serves the agents of each
public sealed class EveryVendorChatClientProvider(ScriptedChatClient client, ProvidedModels models)
    : IChatClientProvider<string>, IChatClientProvider<Vendor>
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        Serve(provider, model);

    public ValueTask<IChatClient> GetChatClientAsync(Vendor provider, string model, CancellationToken ct) =>
        Serve(provider, model);

    private ValueTask<IChatClient> Serve(object provider, string model)
    {
        models.Add(new(provider, model));
        return ValueTask.FromResult<IChatClient>(client);
    }
}
