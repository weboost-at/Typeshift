using Agentic.Agents;
using Agentic.Samples.PackageConsumer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// AddAgentic() only works when the package's generator ran and its props opted the project into the interceptor
var services = new ServiceCollection();

services.AddAgentic();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

var options = await provider.GetRequiredService<AgentRuntime<GreeterAgent>>().ResolveChatOptionsAsync();
var greet = options.Tools!.OfType<AIFunction>().Single();

var greeting = await greet.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["name"] = "package" })
{
    Services = provider
});

Console.WriteLine($"{GreeterAgent.Name} offers {greet.Name}: {greeting}");
