# Stores and settings

Typeshift persists three kinds of data: each agent's `Settings`, each tool's stored defaults, and conversations. All
of it goes through one interface you implement once, with the id type you already use.

## The store

`ITypeshiftStore<TId>` has two methods: `LoadAsync<TData>(id, ct)` returns the data of type `TData` stored under the
id, or null; `SaveAsync<TData>(id, data, ct)` replaces it. `TData` is always an `IStorable<TData>`: it serializes
itself with `Serialize()` and reads itself back with the static `TData.Deserialize(json)`, so a store needs no
knowledge of the data's shape. It keys each record by the data's type and the id: under one id there is one record
per type, an agent's settings next to a tool's defaults and a conversation.

The id type is yours: a `Guid`, a tenant slug, a customer number. The library never creates ids; it passes on the
ones you give it. Conversation ids arrive as strings and are parsed into it, so it must implement `IParsable<TId>`,
as `Guid`, `string`, `int` and `long` do.

## Registering a store

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift();
services.AddTypeshiftStore<InMemoryTypeshiftStore<Guid>, Guid>();
services.AddSingleton<IChatClientProvider, ScriptedChatClientProvider>();
services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(AppJsonContext.Default));

await using var provider = services.BuildServiceProvider();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L13-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshiftStore<TStore, TId>()` registers the store as a singleton; it may depend on singletons such as an
`IDbContextFactory`. `InMemoryTypeshiftStore<TId>` keeps JSON in memory, for tests and samples.

**Without a store**, agents run from their code: `ResolveChatOptions` works, tools run, conversations are tracked
while they run. Every API that loads or saves throws an `InvalidOperationException` that names
`AddTypeshiftStore<TStore, TId>()`, also when the registered store has another id type than the one asked for.

## Writing a store

A store that keeps the JSON as it is fits any database. This one writes one file per record:

<!-- snippet: file-store -->
<a id='snippet-file-store'></a>
```cs
internal sealed class FileTypeshiftStore(IOptions<FileStoreOptions> options) : ITypeshiftStore<Guid>
{
    public async Task<TData?> LoadAsync<TData>(Guid id, CancellationToken ct) where TData : class, IStorable<TData>
    {
        var path = GetPath<TData>(id);

        return File.Exists(path) ? TData.Deserialize(await File.ReadAllTextAsync(path, ct)) : null;
    }

    public async Task SaveAsync<TData>(Guid id, TData data, CancellationToken ct) where TData : class, IStorable<TData>
    {
        var path = GetPath<TData>(id);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, data.Serialize(), ct);
    }

    // One folder per kind of data, one file per id; the full name keeps the Settings of two agents apart
    private string GetPath<TData>(Guid id) => Path.Combine(options.Value.Root, typeof(TData).FullName!, $"{id}.json");
}

internal sealed class FileStoreOptions
{
    public string Root { get; set; } = "data";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/FileTypeshiftStore.cs#L6-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-file-store' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: file-store-registration -->
<a id='snippet-file-store-registration'></a>
```cs
services.Configure<FileStoreOptions>(options => options.Root = root.FullName);
services.AddTypeshiftStore<FileTypeshiftStore, Guid>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/FileStore.cs#L18-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-file-store-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Key records by `typeof(TData).FullName`, not `Name`: every agent's settings type is called `Settings`. Moving or
renaming an agent class changes its full name, and its stored settings with it.

### Columns instead of JSON

The data is typed, so a store may map it to columns of its own instead. This Entity Framework Core store keeps the
support agent's settings in a table with one column per value and everything else as JSON documents:

<!-- snippet: settings-columns -->
<a id='snippet-settings-columns'></a>
```cs
internal sealed class SupportAgentSettingsRow
{
    public required string Tenant { get; init; }

    public string? Model { get; set; }

    public string? Instructions { get; set; }

    public float? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public int? TicketLimit { get; set; }

    public bool? IncludeClosedTickets { get; set; }

    public string? ArticleLanguage { get; set; }

    public static SupportAgentSettingsRow From(string tenant, SupportAgent.Settings settings) =>
        new()
        {
            Tenant = tenant,
            Model = settings.Model,
            Instructions = settings.Instructions,
            Temperature = settings.Temperature,
            MaxOutputTokens = settings.MaxOutputTokens,
            TicketLimit = settings.SearchTickets.Limit,
            IncludeClosedTickets = settings.SearchTickets.IncludeClosed,
            ArticleLanguage = settings.SearchArticles.Language
        };

    public SupportAgent.Settings ToSettings() =>
        new()
        {
            Model = Model,
            Instructions = Instructions,
            Temperature = Temperature,
            MaxOutputTokens = MaxOutputTokens,
            SearchTickets = new TicketToolset.SearchTicketsOverrides
            {
                Limit = TicketLimit,
                IncludeClosed = IncludeClosedTickets
            },
            SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = ArticleLanguage }
        };
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/SupportAgentSettingsRow.cs#L7-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-columns' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: ef-core-store -->
<a id='snippet-ef-core-store'></a>
```cs
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDbContext> contexts) : ITypeshiftStore<string>
{
    public async Task<TData?> LoadAsync<TData>(string tenant, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        // The support agent's settings live in columns of their own, everything else in JSON documents
        if (typeof(TData) == typeof(SupportAgent.Settings))
            return await db.SupportAgentSettings.FindAsync([tenant], ct) is { } row
                ? (TData)(object)row.ToSettings()
                : null;

        return await db.Documents.FindAsync([Kind<TData>(), tenant], ct) is { } document
            ? TData.Deserialize(document.Json)
            : null;
    }

    public async Task SaveAsync<TData>(string tenant, TData data, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        if (data is SupportAgent.Settings settings)
        {
            var row = SupportAgentSettingsRow.From(tenant, settings);
            Upsert(db, await db.SupportAgentSettings.FindAsync([tenant], ct), row);
        }
        else
        {
            var document = new StoredDocument
            {
                Kind = Kind<TData>(),
                Id = tenant,
                Json = data.Serialize()
            };

            Upsert(db, await db.Documents.FindAsync([document.Kind, tenant], ct), document);
        }

        await db.SaveChangesAsync(ct);
    }

    // The full name keeps the Settings of two agents apart
    private static string Kind<TData>() => typeof(TData).FullName!;

    private static void Upsert<TRow>(TypeshiftDbContext db, TRow? existing, TRow row) where TRow : class
    {
        if (existing is null) db.Add(row);
        else db.Entry(existing).CurrentValues.SetValues(row);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L7-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-store' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: ef-core-store-registration -->
<a id='snippet-ef-core-store-registration'></a>
```cs
services.AddDbContextFactory<TypeshiftDbContext>(options => options.UseSqlite(connection));
services.AddTypeshiftStore<EfTypeshiftStore, string>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/EfCoreStore.cs#L22-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-store-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A configurable member added later shows up as a new property on `Settings`; a column store has to map it, while a
JSON store picks it up without a change.

## Agent settings

The generated runtime members load and save an agent's `Settings` under an id. `ConfigureAsync` loads them, applies
your change and saves the result:

<!-- snippet: configure-settings -->
<a id='snippet-configure-settings'></a>
```cs
var customer = Guid.NewGuid();

await support.ConfigureAsync(customer, settings => settings with
{
    Model = "reasoning-model",
    Instructions = "Answer in German.",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});

var options = await support.ResolveChatOptionsAsync(customer);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L16-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-configure-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`LoadSettingsAsync` returns empty settings when none are stored; `SaveSettingsAsync` replaces what is stored. Setting
a value back to null hands it back to the layer below:

<!-- snippet: load-and-save-settings -->
<a id='snippet-load-and-save-settings'></a>
```cs
var settings = await support.LoadSettingsAsync(customer);

await support.SaveSettingsAsync(customer, settings with { Instructions = null });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L31-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-and-save-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

What is stored is the record's JSON. Values left null are left out, and so is a tool without any value:

<!-- snippet: samples/Typeshift.Samples.Docs/Examples/support-agent-settings.json -->
<a id='snippet-samples/Typeshift.Samples.Docs/Examples/support-agent-settings.json'></a>
```json
{
  "model": "reasoning-model",
  "tools": {
    "search_tickets": {
      "limit": 20
    }
  }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Examples/support-agent-settings.json#L1-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-samples/Typeshift.Samples.Docs/Examples/support-agent-settings.json' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Tool defaults

`ToolDefaults` stores the defaults of a tool's configurable settings, taking the tool by its
[reference](./toolsets.md#tool-references):

<!-- snippet: tool-defaults -->
<a id='snippet-tool-defaults'></a>
```cs
var defaults = services.GetRequiredService<ToolDefaults>();

await defaults.ConfigureAsync(customer, KnowledgeToolset.Tools.SearchArticles, articles => articles with
{
    Language = "de"
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/ToolDefaultsScenario.cs#L17-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-tool-defaults' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Tool defaults are stored under the same id as agent settings. They apply to every agent run under that id whose code
does not set the tool's settings (the support agent sets those of `search_tickets`, so only an agent's stored
settings or a run may change them).

## Concurrency and versions

`ConfigureAsync` loads, changes and saves without a version check: of two concurrent updates under one id, the later
save wins. The JSON carries no schema version; a renamed configurable member reads as null, so its stored value falls
back to the layer below.
