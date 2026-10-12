using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Samples.Chat;

// A real host maps each model to a provider client here
internal sealed class ScriptedChatClientProvider : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(new ScriptedChatClient(model));
}
