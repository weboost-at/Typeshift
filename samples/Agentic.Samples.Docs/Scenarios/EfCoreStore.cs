using Agentic.Agents;
using Agentic.Samples.Docs.Agents;
using Agentic.Samples.Docs.Knowledge;
using Agentic.Samples.Docs.Stores.EfCore;
using Agentic.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Scenarios;

internal static class EfCoreStore
{
    public static async Task RunAsync()
    {
        // An in-memory SQLite database lives as long as its connection
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var services = Host.Create(services =>
        {
            // begin-snippet: ef-core-store-registration
            services.AddDbContextFactory<AgenticDbContext>(options => options.UseSqlite(connection));
            services.AddAgenticStore<EfAgenticStore, string>();
            // end-snippet
        });

        await using (var db = await services.GetRequiredService<IDbContextFactory<AgenticDbContext>>().CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();

        await support.ConfigureAsync("acme", settings => settings with
        {
            Temperature = 0.1f,
            SearchTickets = settings.SearchTickets with { Limit = 7 }
        });

        await services.GetRequiredService<ToolDefaults>()
            .ConfigureAsync("acme", KnowledgeToolset.Tools.SearchArticles, articles => articles with { Language = "de" });

        var options = await support.ResolveChatOptionsAsync("acme");

        await using var check = await services.GetRequiredService<IDbContextFactory<AgenticDbContext>>()
            .CreateDbContextAsync();

        Expect.That(options.Temperature == 0.1f, "settings resolve from their columns");
        Expect.That((await check.SupportAgentSettings.SingleAsync()).TicketLimit == 7, "settings are stored as columns");
        Expect.That(await check.Documents.CountAsync() == 1, "tool defaults are stored as a document");
    }
}
