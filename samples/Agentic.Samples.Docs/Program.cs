using Agentic.Samples.Docs.Scenarios;

// Runs what the documentation shows; CI runs it, so a snippet that stops working fails the build
await RunningAgents.RunAsync();
await AgentSettings.RunAsync();
await ToolDefaultsScenario.RunAsync();
await Conversations.RunAsync();
await FileStore.RunAsync();
await EfCoreStore.RunAsync();

Console.WriteLine("Every documented scenario ran.");
