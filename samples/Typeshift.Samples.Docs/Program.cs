using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Scenarios;

// Runs every scenario the guide shows; a scenario that breaks throws, which fails CI. The toolsets of the guide are
// checked at compile time, by the analyzer, and need no scenario of their own.
var client = new CannedChatClient();
var services = Registration.Register(new ServiceCollection(), client, client);

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

await RunningAgents.RunAsync(provider);

return 0;
