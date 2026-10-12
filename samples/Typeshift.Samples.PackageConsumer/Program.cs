using Typeshift.Agents;
using Typeshift.Samples.PackageConsumer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// AddTypeshift() only works when the package's generator ran and its props opted the project into the interceptor
var services = new ServiceCollection();

services.AddTypeshift();

await using var provider = services.BuildServiceProvider();

var greeter = provider.GetRequiredService<AgentRuntime<GreeterAgent>>();
var greet = greeter.ResolveChatOptions().Tools!.OfType<AIFunction>().Single();

var greeting = await greet.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["name"] = "package" })
{
    Services = provider
});

Console.WriteLine($"{greeter.Definition.Name} offers {greet.Name}: {greeting}");
