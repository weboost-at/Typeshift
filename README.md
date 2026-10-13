# Typeshift

Code-defined agents and toolsets for .NET 10 on
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). A source generator turns
them into reflection-free `AIFunction`s, a typed settings record per agent, and one `services.AddTypeshift()` call.
Everything runs under Native AOT.

[![ci](https://github.com/weboost-at/Typeshift/actions/workflows/ci.yml/badge.svg)](https://github.com/weboost-at/Typeshift/actions/workflows/ci.yml)

**[Documentation](https://weboost-at.github.io/Typeshift/)** ·
[Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) ·
[Diagnostics](https://weboost-at.github.io/Typeshift/guide/diagnostics) ·
[Changelog](CHANGELOG.md)

> **Alpha.** The API will change between releases.

## Features

- **Tools from static methods.** Each tool becomes a generated `AIFunction` with a JSON schema from its parameters and
  XML docs; arguments bind and results serialize without reflection.
- **Typed, layered settings.** A tool's settings resolve from code defaults, stored defaults, the agent's code, the
  agent's stored settings and the run, and reach the tool as a parameter the model never sees. Only members the code
  marks configurable can change.
- **A settings contract per agent.** The generator writes a `Settings` record per agent holding exactly what may
  change at runtime, model included: store it per customer, pass it per run, or bind a UI to it.
- **One store, your ids.** Implement `ITypeshiftStore<TId>` once for your database, with the id type you already use.
  Without a store, agents run from code.
- **Conversation tracking.** Every exchange with the model is tracked live and stored as a session of its
  conversation, streaming included.
- **Plain fluent C#.** Tools, agents, chat options and tool settings are each declared in one chain, with
  instructions in an overridable `GetInstructions()`.
- **Mistakes fail the build.** Including a tool of the wrong toolset or setting the settings of a tool the agent does
  not offer does not compile, and the generator's diagnostics cover the rest.

## Install

The package is not on nuget.org yet. Until the first release, pack it from source and add the folder as a package
source:

```shell
dotnet pack src/Typeshift -c Release -o artifacts/packages
dotnet nuget add source "$PWD/artifacts/packages" --name typeshift-local
```

Add it to every project that declares toolsets or agents, or calls `AddTypeshift()`: the package carries the generator,
and NuGet does not pass generators on to referencing projects. Requires the .NET 10 SDK and C# 14.

## Quickstart

A toolset declares its tools in its constructor; a tool is a static method.

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

An agent picks its chat options, tools and tool settings. `t.FindCompanies` and `ConfigureFindCompanies()` are generated
per tool.

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

Register everything, store settings for a customer, and run. `IChatClientProvider` maps model names to your
`IChatClient`s; this sample uses a scripted one.

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

<!-- snippet: quickstart-settings -->
<a id='snippet-quickstart-settings'></a>
```cs
var research = provider.GetRequiredService<AgentRuntime<ResearchAgent>>();
var customer = Guid.NewGuid();

await research.ConfigureAsync(customer, settings => settings with
{
    Model = "scripted-model",
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L22-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var conversationId = Guid.NewGuid();
var options = await research.ResolveChatOptionsAsync(customer, conversationId: conversationId.ToString());
var client = await research.CreateChatClientAsync(options);
var answer = await client.GetResponseAsync("Vienna", options);

Console.WriteLine($"{research.Definition.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L35-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The code above is the [console sample](samples/Typeshift.Samples.Console), which CI publishes as Native AOT and runs.
The [getting started guide](https://weboost-at.github.io/Typeshift/guide/getting-started) walks through it.

## Building from source

```shell
dotnet tool restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
```

[AGENTS.md](AGENTS.md) holds the rules for code, tests and commits; the
[contributing docs](https://weboost-at.github.io/Typeshift/contributing/) explain the architecture and the decisions
behind it.

## License

[MIT](LICENSE)
