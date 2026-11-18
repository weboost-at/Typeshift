using Typeshift;
using Typeshift.Samples.PackageConsumer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// AddTypeshift() works only when the package's generator ran and its props opted the project into the interceptor
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

var greet = tools.Single(tool => tool.Name == "greet");
var greeting = await greet.InvokeAsync(arguments);

// The package turned XML docs on, so the tool carries its summary, the type it takes carries its own, and the
// undocumented Visitor below fails nothing
if (greet.Description != "Greets a person by name.")
    throw new InvalidOperationException($"Expected the tool's XML docs as its description, got '{greet.Description}'.");

var welcome = tools.Single(tool => tool.Name == "welcome").JsonSchema.GetRawText();
if (!welcome.Contains("The name the guest gave.", StringComparison.Ordinal))
    throw new InvalidOperationException($"Expected the guest's XML docs in the tool's schema, got {welcome}.");

Console.WriteLine($"{greeter.Agent.Name} offers {string.Join(", ", tools.Select(tool => tool.Name))}: {greeting}");

// A run through the generated members, on a scripted model: the model calls the tool, and the temperature stored
// with UpdateSettingsAsync reaches its call
var model = new ScriptedClient();

await using var runtime = TypeshiftRuntime.Create(
    model,
    typeshift => typeshift.Services.AddKeyedChatClient("any-provider", model));

await using var agent = runtime.CreateHost<GreeterAgent>();

await agent.UpdateSettingsAsync(settings => settings with { Temperature = 0.5f });
var answer = await agent.RunAsync("Greet Ada.");

if (answer.Text != "Hello, Ada!")
    throw new InvalidOperationException($"Expected the run to answer with the tool's greeting, got '{answer.Text}'.");

if (model.Temperature != 0.5f)
    throw new InvalidOperationException($"Expected the stored temperature on the call, got {model.Temperature}.");

Console.WriteLine($"{agent.Agent.Name} ran: {answer.Text}");

public sealed record Visitor(string Name);
