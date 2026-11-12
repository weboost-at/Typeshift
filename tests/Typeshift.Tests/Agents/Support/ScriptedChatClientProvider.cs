using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents.Support;

// Serves every model from the test's scripted client and records the models it was asked for
public sealed class ScriptedChatClientProvider(ScriptedChatClient client, ProvidedModels models)
    : IChatClientProvider<string>
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct)
    {
        models.Add(new(provider, model));
        return ValueTask.FromResult<IChatClient>(client);
    }
}

public sealed class ProvidedModels : List<ProvidedModel>;

// A provider of any type, a string or an enum, compared by its value
public sealed record ProvidedModel(object Provider, string Model);
