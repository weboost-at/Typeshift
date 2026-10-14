using Typeshift;
using Typeshift.Samples.PackageConsumer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// AddTypeshift() works only when the package's generator ran and its props opted the project into the interceptor;
// the built-in toolsets come from the package's own module
var services = new ServiceCollection();

services.AddTypeshift();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var greeter = scope.ServiceProvider.GetRequiredService<AgentHost<GreeterAgent>>();
var options = await greeter.ResolveOptionsAsync();
var tools = options.Tools!.OfType<AIFunction>().ToList();

var arguments = new AIFunctionArguments(new Dictionary<string, object?> { ["name"] = "package" })
{
    Services = scope.ServiceProvider
};

var greeting = await tools.Single(tool => tool.Name == "greet").InvokeAsync(arguments);

Console.WriteLine($"{greeter.Agent.Name} offers {string.Join(", ", tools.Select(tool => tool.Name))}: {greeting}");
