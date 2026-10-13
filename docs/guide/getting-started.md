# Getting started

This page builds a working agent: a toolset with one tool, an agent that offers it, the registration, and a run with
stored settings. The code is the console sample in
[`samples/Typeshift.Samples.Console`](https://github.com/weboost-at/Typeshift/tree/main/samples/Typeshift.Samples.Console),
which CI publishes as Native AOT and runs.

## Install

Typeshift targets .NET 10 and C# 14. The package is not on nuget.org yet. Until the first release, pack it from a
clone of the repository and add the folder as a package source:

```shell
dotnet pack src/Typeshift -c Release -o artifacts/packages
dotnet nuget add source "$PWD/artifacts/packages" --name typeshift-local
```

Add the package to **every project that declares toolsets or agents, or calls `AddTypeshift()`**. The package carries
the source generator, and NuGet does not pass a generator on to the projects that reference yours. A referenced
project without it is reported as [TSH011](./diagnostics.md#tsh011).

## A toolset

A toolset is a `partial` class deriving from `Toolset` that declares its tools in its constructor. A tool is a public
static method with a lower_snake_case name.

<!-- snippet: quickstart-toolset -->
<a id='snippet-quickstart-toolset'></a>
```cs
internal partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        DisplayName = "Companies";

        AddTool(FindCompanies)
            .WithName("find_companies")
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);
    }

    /// <summary>
    /// Finds the companies of a city, best matches first.
    /// </summary>
    /// <param name="city">The city the companies are based in.</param>
    /// <param name="settings">The settings the search runs with; the model never sees them.</param>
    public static IReadOnlyList<Company> FindCompanies(string city, FindCompaniesSettings settings) =>
    [
        .. KnownCompanies.All
            .Where(company => company.City.Equals(city, StringComparison.OrdinalIgnoreCase))
            .Where(company => company.Active || settings.IncludeInactive)
            .Take(settings.Limit)
    ];
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Companies/CompanyToolset.cs#L5-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The method's `<summary>` becomes the tool's description and each `<param>` the description of its parameter. The
`FindCompaniesSettings` parameter never reaches the model: the call fills it with the tool's resolved settings. The
settings are a record; `Configurable(s => s.Limit)` marks `Limit` as changeable at runtime, while `IncludeInactive`
stays whatever the code says.

<!-- snippet: quickstart-settings-record -->
<a id='snippet-quickstart-settings-record'></a>
```cs
internal record FindCompaniesSettings
{
    public int Limit { get; init; } = 5;

    public bool IncludeInactive { get; init; }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Companies/FindCompaniesSettings.cs#L3-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## An agent

An agent is a `partial` class deriving from `AgentDefinition`. Its constructor sets its chat options, picks its tools
and sets their settings; `GetInstructions()` returns its instructions.

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>
```cs
internal partial class ResearchAgent : AgentDefinition
{
    public ResearchAgent()
    {
        WithName("research_agent");
        WithDescription("Researches the companies of a city.");

        WithChatOptions()
            .WithTemperature(0.2f)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.Instructions);

        UseToolset<CompanyToolset>().Include(t => t.FindCompanies);

        ConfigureFindCompanies()
            .WithLimit(3);
    }

    protected override string GetInstructions() =>
        """
        Find companies with the find_companies tool and list them.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Companies/ResearchAgent.cs#L5-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`t.FindCompanies` is a property the generator adds for each tool of `CompanyToolset`, and `ConfigureFindCompanies()`
exists because the agent offers that tool, with a `With...` per member of its settings. Both reject mistakes at
compile time.

## Registration

`AddTypeshift()` registers every toolset and agent of the project and of the projects it references. The store is
optional: without one, everything runs from code. The chat client provider maps model names to `IChatClient`s; the
sample uses a scripted one, a real host [plugs in its providers](./running-agents.md#chat-clients).

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift();
services.AddTypeshiftStore<InMemoryTypeshiftStore<Guid>, Guid>();
services.AddSingleton<IChatClientProvider, ScriptedChatClientProvider>();

await using var provider = services.BuildServiceProvider();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L11-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Settings per customer

The generator gives `ResearchAgent` a nested `Settings` record holding exactly what may change: the model, the
configurable chat options and the configurable settings of its tools. Here they are stored under a customer's id.

<!-- snippet: quickstart-settings -->
<a id='snippet-quickstart-settings'></a>
```cs
var customer = Guid.CreateVersion7(time.GetUtcNow());

await research.ConfigureAsync(customer, settings => settings with
{
    Model = "scripted-model",
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L25-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## A run

`ResolveChatOptionsAsync` layers the stored settings over the code and adds the agent's tools.
`CreateChatClientAsync` picks the client for the resolved model and wraps it in the tool-calling loop and
conversation tracking.

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var conversationId = Guid.CreateVersion7(time.GetUtcNow());
var options = await research.ResolveChatOptionsAsync(customer, conversationId: conversationId.ToString());
var client = await research.CreateChatClientAsync(options);
var answer = await client.GetResponseAsync("Vienna", options);

Console.WriteLine($"{research.Definition.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L37-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Because the run had a conversation id, the tracked conversation is stored under it:

<!-- snippet: quickstart-conversation -->
<a id='snippet-quickstart-conversation'></a>
```cs
var store = provider.GetRequiredService<ITypeshiftStore<Guid>>();

var conversation = await store.LoadAsync<StoredConversation>(conversationId, CancellationToken.None)
    ?? throw new InvalidOperationException("The conversation was not stored.");

Console.WriteLine(
    $"Stored {conversation.AggregatedMessages.Count} messages of {conversation.ExecutionContext.Value}, " +
    $"{conversation.AggregatedUsage.TotalTokenCount} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L47-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A single run can also override settings without storing them, here streamed:

<!-- snippet: quickstart-run-settings -->
<a id='snippet-quickstart-run-settings'></a>
```cs
var run = new ResearchAgent.Settings
{
    Model = "scripted-model",
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
};

var runOptions = research.ResolveChatOptions(run);
var runClient = await research.CreateChatClientAsync(runOptions);

Console.Write($"{research.Definition.Name}, one run: ");

await foreach (var update in runClient.GetStreamingResponseAsync("Vienna", runOptions))
    Console.Write(update.Text);

Console.WriteLine();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L59-L75' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The sample prints:

```text
research_agent: Found Danube Robotics, Prater Motion.
Stored 4 messages of research_agent, 300 tokens
research_agent, one run: Found Danube Robotics.
```

## Next steps

- [Toolsets](./toolsets.md) and [tool settings](./tool-settings.md) in depth.
- [Agents](./defining-agents.md) and [running them](./running-agents.md) with real providers.
- [Stores and settings](./persistence.md): implementing `ITypeshiftStore` for your database.
- [Native AOT](./native-aot.md): what a host declares to publish without warnings.
