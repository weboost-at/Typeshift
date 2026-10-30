using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Quickstart;
using Typeshift.Samples.Scenarios;
using Typeshift.Samples.Stores.EfCore;

// Runs every scenario the guide shows; a scenario that breaks throws, which fails CI. The toolsets of the guide are
// checked at compile time, by the analyzer, and need no scenario of their own.
await Quickstart.RunAsync();

var database = Path.Combine(Path.GetTempPath(), $"typeshift-samples-{Guid.NewGuid():N}.db");
var client = new CannedChatClient();
var services = Registration.Register(new ServiceCollection(), client, client, database);

await using (var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
}))
{
    await using (var db = await provider.GetRequiredService<IDbContextFactory<TypeshiftDb>>().CreateDbContextAsync())
    {
        await db.Database.EnsureCreatedAsync();
    }

    await StoreVerification.RunAsync(provider);
    await AgentSettings.RunAsync(provider);
    await RunningAgents.RunAsync(provider);
    await Conversations.RunAsync(provider, CancellationToken.None);
    await Approval.RunAsync(provider);
    await Modes.RunAsync(provider);
    await Middleware.RunAsync(provider);
    await MultiAgent.RunAsync(provider);
}

var web = Path.Combine(Path.GetTempPath(), $"typeshift-samples-web-{Guid.NewGuid():N}.db");
await ChatEndpoint.RunAsync(client, web);

Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
File.Delete(database);
File.Delete(web);

return 0;
