using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Hosting;

internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider<string>
{
    // One client per provider serves all its models: Typeshift puts the model into every request
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult(
            services.GetKeyedService<IChatClient>(provider)
                ?? throw new InvalidOperationException($"No IChatClient is registered for the provider '{provider}'."));
}
