# Getting started

This page builds a working agent: a toolset with one tool, an agent that offers it, the registration, stored settings
and a run.

## Install

In a .NET 10 project, add the package and set `GenerateDocumentationFile` to `true`: tools take their descriptions
from XML docs.

```bash
dotnet add package Typeshift --prerelease
```

A settings id names an agent's stored settings, a conversation id its conversation. This page uses `int` ids, set
once for the build in `Directory.Build.props`:

<!-- snippet: id-type -->
<a id='snippet-id-type'></a>
```csproj
<PropertyGroup>
  <TypeshiftIdType>int</TypeshiftIdType>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Typeshift.Samples.Docs.csproj#L5-L9' title='Snippet source file'>snippet source</a> | <a href='#snippet-id-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[Registration](./registration.md#project-setup) lists what every project needs and the id types it takes.

## A toolset

A toolset is a `partial` class deriving from `Toolset` that declares its tools in its constructor. A tool is a
static method; its name is the method's in snake_case, `find_companies`.

<!-- snippet: quickstart-toolset -->
<a id='snippet-quickstart-toolset'></a>
```cs
internal partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        WithDisplayName("Companies");

        AddTool(FindCompanies)
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);
    }

    /// <summary>
    /// Finds the companies of a city, best matches first.
    /// </summary>
    /// <param name="city">The city the companies are based in.</param>
    public static IReadOnlyList<Company> FindCompanies(string city, FindCompaniesSettings settings) =>
    [
        .. Companies
            .Where(company => company.City.Equals(city, StringComparison.OrdinalIgnoreCase))
            .Where(company => company.Active || settings.IncludeInactive)
            .Take(settings.Limit)
    ];
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/CompanyToolset.cs#L3-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The `<summary>` becomes the tool's description and each `<param>` its parameter's. The `FindCompaniesSettings`
parameter never reaches the model: the call fills it with the tool's resolved settings, a record whose code sets the
defaults. `Configurable(s => s.Limit)` lets `Limit` change at runtime; `IncludeInactive` stays what the code says.

<!-- snippet: quickstart-settings-record -->
<a id='snippet-quickstart-settings-record'></a>
```cs
internal record FindCompaniesSettings
{
    public int Limit { get; init; } = 5;

    public bool IncludeInactive { get; init; }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/FindCompaniesSettings.cs#L3-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## An agent

An agent is a `partial` class deriving from `Agent`. Its constructor sets its chat options, picks its tools and sets
their settings; `GetInstructions()` returns its instructions.

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>
```cs
internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches the companies of a city.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.2f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<CompanyToolset>();

        ConfigureFindCompanies()
            .WithLimit(3);
    }

    protected override string GetInstructions() =>
        """
        Find companies with the find_companies tool and list them.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/ResearchAgent.cs#L3-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ConfigureFindCompanies()` exists because the agent offers `find_companies`, with a `With...` per member of its
settings. Configuring a tool the agent does not offer does not compile.

## Registration

`AddTypeshift()` registers every toolset and agent of the project and the projects it references. The chain adds the
store, which turns persistence on, and the provider that maps model names to `IChatClient`s.

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>()
    .WithChatClientProvider<CannedChatClientProvider>();

await using var provider = services.BuildServiceProvider(validateScopes: true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L10-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Stored settings

The generator gives `ResearchAgent` a nested `Settings` record holding exactly what may change: the configurable
chat options and the configurable settings of its tools. `AgentHost<ResearchAgent>` stores them under a settings id,
one per customer say.

<!-- snippet: quickstart-settings -->
<a id='snippet-quickstart-settings'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var research = scope.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>();
var settingsId = 42;

await research.ConfigureAsync(settingsId, settings => settings with
{
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L20-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## A run

`RunAsync` layers the settings stored under the settings id over the code, resumes the conversation, runs the tool
loop and stores the exchange:

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var answer = await research.RunAsync("Vienna", settingsId, conversationId: 1);

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L32-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A run can also pass settings for itself alone, here streamed. [Settings layering](./settings-layering.md) follows
the limit of `find_companies` through every layer:

<!-- snippet: quickstart-run-settings -->
<a id='snippet-quickstart-run-settings'></a>
```cs
var run = new ResearchAgent.Settings
{
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
};

await foreach (var update in research.RunStreamingAsync("Vienna", settingsId, settings: run))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L38-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Next steps

- [Toolsets](./toolsets.md) and [tool settings](./tool-settings.md) in depth.
- [Agents](./defining-agents.md) and [running them](./running-agents.md) with real providers.
- [Settings and stores](./settings-and-stores.md): implementing `ITypeshiftStore` for your database.
- [Conversations](./conversations.md): resuming, steering and watching runs.
- [Why code-defined](./why-code-defined.md): what the generator buys and what it costs.
- The reference: [registration](./registration.md), [builders](./builders.md),
  [generated members](./generated-members.md) and the [agent host](./agent-host.md).
