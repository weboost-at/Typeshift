using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents.Support;

// Serves every model from the test's scripted client and records the models it was asked for
public sealed class ScriptedChatClientProvider(ScriptedChatClient client, ProvidedModels models) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct)
    {
        models.Add(model);
        return ValueTask.FromResult<IChatClient>(client);
    }
}

public sealed class ProvidedModels : List<string>;
