using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Orders;
using Typeshift.Samples.Quickstart;
using Typeshift.Samples.Scenarios;
using Typeshift.Samples.Stores.EfCore;

// Runs what the guide shows: every agent, toolset, rule and provider of the pages, each scenario checking what its
// page says, and every registration the pages show, built and resolved. A scenario that breaks throws, which fails
// CI. Only a real provider's call stays compile-only: its clients are built, and nothing calls them
var client = new CannedChatClient();

await Quickstart.RunAsync(client);

var settings = Path.Combine(Path.GetTempPath(), $"typeshift-samples-settings-{Guid.NewGuid():N}");
await FileSettings.RunAsync(settings);
Directory.Delete(settings, recursive: true);

var database = Path.Combine(Path.GetTempPath(), $"typeshift-samples-{Guid.NewGuid():N}.db");
var shop = Path.Combine(Path.GetTempPath(), $"typeshift-samples-shop-{Guid.NewGuid():N}.db");

await NoContainer.RunAsync(client);
await Catalog.RunAsync(client);
await SmallestAgent.RunAsync(client);
await KeepEveryConversation.RunAsync(client);
await Testing.RunAsync();
await OutputGuardrail.RunAsync();
await MinimalApi.RunAsync(client);
var services = Registration.Register(new ServiceCollection(), client, client, database);
services.AddSingleton<SampleLog>();
services.AddSingleton<ILoggerProvider>(registered => registered.GetRequiredService<SampleLog>());
services.AddDbContextFactory<ShopDb>(options => options.UseSqlite($"Data Source={shop}"));

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
    await RunningAgents.RunAsync(provider, CancellationToken.None);
    await Conversations.RunAsync(provider, CancellationToken.None);
    await Approval.RunAsync(provider);
    await Questions.RunAsync(provider);
    await Modes.RunAsync(provider);
    await Middleware.RunAsync(provider);
    await MultiAgent.RunAsync(provider);
    await LongRunning.RunAsync(provider);
    await Toolsets.RunAsync(provider);
    await ConcurrentToolCalls.RunAsync(provider);
    await Mcp.RunAsync(provider);
    await ChatHub.RunAsync(provider);
    await Observability.RunAsync(provider);
    await AgentFramework.RunAsync(provider);
}

await ShownRegistrations.RunAsync(client);

var web = Path.Combine(Path.GetTempPath(), $"typeshift-samples-web-{Guid.NewGuid():N}.db");
await ChatEndpoint.RunAsync(client, web);

Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
File.Delete(database);
File.Delete(shop);
File.Delete(web);

return 0;
