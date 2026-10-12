# Agentic

Code-defined agents and toolsets for .NET 10 on
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). A Roslyn source generator
turns them into reflection-free `AIFunction`s, typed and allowlisted settings, and one `services.AddAgentic()`
registration. Everything works under Native AOT.

**Status:** `0.1.0-alpha.1`. The API will change; `docs/status.md` lists what is done and what is open.

## What you get

- **Toolsets:** static methods become tools. The generator writes one `AIFunction` per tool, with a JSON schema built
  from the parameters and their XML docs, and binds the model's arguments without reflection.
- **Tool settings:** a tool reads a settings record from its call's services. Each tool declares which members may
  change at runtime; stored defaults, agent settings and per-run overrides layer on top of the code defaults.
- **Agents:** an agent picks its tools, its chat options and its tool settings in code. The generator gives it a typed
  configuration view that only exposes what the code marks configurable.
- **Stored agents:** agents defined as data run through the same path as code agents.
- **Conversation tracking:** every model exchange is stored as a session of a conversation, behind
  `IConversationStore`, streaming included.
- **One registration call:** `services.AddAgentic()` registers the toolsets and agents of the calling project and of
  every project it references, checked at compile time by sixteen diagnostics (`docs/diagnostics.md`).

## Quickstart

The quickstart is the console sample in `samples/Agentic.Samples.Console`, which CI publishes as Native AOT and runs.
A test keeps the blocks below identical to the sample's files.

Reference the package from every project that declares toolsets or agents or calls `AddAgentic()`. NuGet does not
pass a package's generator on to the projects that reference yours, so a toolset in a project without the package
is never generated (diagnostic AGT014 tells you which reference misses it).

```xml
<PackageReference Include="Agentic" Version="0.1.0-alpha.1" />
```

The package is not on NuGet.org yet: `dotnet pack src/Agentic -c Release -o artifacts/packages` and add that folder
as a package source, as `samples/Agentic.Samples.PackageConsumer` does.

### A toolset

A toolset is a `partial` class that adds its tools in the constructor. The tool's description comes from
`WithDescription(...)` or the method's `<summary>`, the parameter descriptions from `<param>`. Parameters of type
`IServiceProvider`, `CancellationToken` and `AIFunctionArguments` are filled by the call and never reach the model.

<!-- sample: samples/Agentic.Samples.Console/Companies/CompanyToolset.cs -->
```csharp
using Agentic.Tools;
using Agentic.Toolsets;

namespace Agentic.Samples.Companies;

internal partial class CompanyToolset : Toolset
{
    private static readonly Company[] KnownCompanies =
    [
        new("Danube Robotics", "Vienna", Active: true),
        new("Prater Motion", "Vienna", Active: true),
        new("Ringstrasse Automation", "Vienna", Active: false),
        new("Alpine Grippers", "Graz", Active: true),
        new("Wienerwald Drives", "Vienna", Active: true)
    ];

    public CompanyToolset()
    {
        DisplayName = "Companies";

        AddTool(FindCompanies)
            .WithName("find_companies")
            .WithSettings<FindCompaniesSettings>(settings => settings
                .Configurable(s => s.Limit));
    }

    /// <summary>
    /// Finds the companies of a city, best matches first.
    /// </summary>
    /// <param name="city">The city the companies are based in.</param>
    /// <param name="services">The services of the call.</param>
    public static IReadOnlyList<Company> FindCompanies(string city, IServiceProvider services)
    {
        var settings = services.GetToolSettings<FindCompaniesSettings>();

        return
        [
            .. KnownCompanies
                .Where(company => company.City.Equals(city, StringComparison.OrdinalIgnoreCase))
                .Where(company => company.Active || settings.IncludeInactive)
                .Take(settings.Limit)
        ];
    }
}
```

Settings are a record. `Configurable(...)` lists the members that stored values and run overrides may change; the
others keep what the code says.

<!-- sample: samples/Agentic.Samples.Console/Companies/FindCompaniesSettings.cs -->
```csharp
namespace Agentic.Samples.Companies;

internal record FindCompaniesSettings
{
    public int Limit { get; init; } = 5;

    public bool IncludeInactive { get; init; }
}
```

<!-- sample: samples/Agentic.Samples.Console/Companies/Company.cs -->
```csharp
namespace Agentic.Samples.Companies;

internal record Company(string Name, string City, bool Active);
```

### An agent

An agent is a `partial` class too. It names itself, picks its tools (a whole toolset, or single methods with
`UseToolset<T>(T.Method, ...)`), and sets the chat options and tool settings it runs with.

<!-- sample: samples/Agentic.Samples.Console/Companies/ResearchAgent.cs -->
```csharp
using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Samples.Companies;

internal partial class ResearchAgent : AgentDefinition
{
    public ResearchAgent()
    {
        WithUniqueName("research_agent");
        WithDescription("Researches the companies of a city.");

        var options = new ChatOptions
        {
            Instructions = "Find companies with the find_companies tool and list them.",
            Temperature = 0.2f
        };

        WithChatOptions(options, picker => picker
            .Configurable(o => o.Temperature)
            .Configurable(o => o.Instructions));

        UseToolset<CompanyToolset>();

        WithToolSettings(new FindCompaniesSettings { Limit = 3 });
    }
}
```

### JSON for Native AOT

Tool parameters and results serialize through `JsonTypeInfo` only. Add a source-generated context for your own types
to `AgentJsonOptions.TypeInfoResolverChain`; it is asked before the library's own context and the defaults of
Microsoft.Extensions.AI.

<!-- sample: samples/Agentic.Samples.Console/AppJsonContext.cs -->
```csharp
using System.Text.Json.Serialization;
using Agentic.Samples.Companies;

namespace Agentic.Samples;

// Tool parameters and results serialize through source-generated metadata, which keeps the sample AOT-safe
[JsonSerializable(typeof(IReadOnlyList<Company>))]
internal partial class AppJsonContext : JsonSerializerContext;
```

### The host

`AddAgentic()` registers everything; the stores are yours to register (in-memory ones ship for tests and samples), and
so is the `IChatClientProvider` that maps a model name to a chat client. The sample's provider hands out a scripted
client that calls `find_companies` and answers with its result; a real host returns a provider's client here.

<!-- sample: samples/Agentic.Samples.Console/Program.cs -->
```csharp
using System.Text.Json;
using Agentic.Agents;
using Agentic.Catalogs;
using Agentic.Conversations;
using Agentic.Json;
using Agentic.Persistence;
using Agentic.Persistence.InMemory;
using Agentic.Samples;
using Agentic.Samples.Chat;
using Agentic.Samples.Companies;
using Agentic.StoredAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Registration: one call registers every toolset and agent of this assembly and of the assemblies it references
var services = new ServiceCollection();

services.AddAgentic();
services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(AppJsonContext.Default));

services.AddSingleton<IChatClientProvider, ScriptedChatClientProvider>();
services.AddSingleton<IAgentConfigurationStore, InMemoryAgentConfigurationStore>();
services.AddSingleton<IToolDefaultsStore, InMemoryToolDefaultsStore>();
services.AddSingleton<IAgentDefinitionStore, InMemoryAgentDefinitionStore>();
services.AddSingleton<IConversationStore, InMemoryConversationStore>();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

var time = provider.GetRequiredService<TimeProvider>();
var conversations = provider.GetRequiredService<IConversationStore>();
var json = provider.GetRequiredService<AgentJson>();

// Typed settings: only what the agent's code marks configurable can be stored
var research = provider.GetRequiredService<AgentRuntime<ResearchAgent>>();

await research.ConfigureAsync(configuration =>
{
    configuration.Model = "scripted-model";
    configuration.Temperature = 0.4f;
    configuration.FindCompanies.Limit = 2;
});

// A tool loop: the model calls find_companies, which runs with the resolved settings
var conversationId = Guid.CreateVersion7(time.GetUtcNow());

var run = new RunOverrides
{
    ChatOptions = new ChatOptions { ConversationId = conversationId.ToString() }
};

var client = await research.CreateChatClientAsync();
var options = await research.ResolveChatOptionsAsync(run);
var answer = await client.GetResponseAsync("Vienna", options);

Console.WriteLine($"{ResearchAgent.Name}: {answer.Text}");

// A tracked conversation: every exchange is stored under the agent that ran it, as a document a store can persist
var conversation = await conversations.LoadAsync(conversationId, CancellationToken.None)
    ?? throw new InvalidOperationException("The conversation was not stored.");

var document = JsonSerializer.Serialize(conversation, json.GetTypeInfo<StoredConversation>());
var restored = JsonSerializer.Deserialize(document, json.GetTypeInfo<StoredConversation>())!;

Console.WriteLine(
    $"Stored {restored.AggregatedMessages.Count} messages of {restored.ExecutionContext.Value}, " +
    $"{restored.AggregatedUsage.TotalTokenCount} tokens, read back from JSON");

// A stored agent: defined as data, it runs through the same path as a code agent
var catalog = provider.GetRequiredService<AgentCatalog>();

var scout = StoredAgent.Create("Market scout", time) with
{
    Description = "Scouts the active companies of a city.",
    Tools = [CompanyToolset.Tools.FindCompanies.Name]
};

await catalog.SaveAsync(scout);

var scoutRuntime = await catalog.GetRuntimeAsync(scout.UniqueName);
await scoutRuntime.UpdateConfigurationAsync(configuration => configuration with { Model = "scripted-model" });

var scoutClient = await scoutRuntime.CreateChatClientAsync();
var scoutOptions = await scoutRuntime.ResolveChatOptionsAsync();

Console.Write($"{scout.DisplayName}: ");

await foreach (var update in scoutClient.GetStreamingResponseAsync("Graz", scoutOptions))
    Console.Write(update.Text);

Console.WriteLine();
```

It prints:

```text
research_agent: Found Danube Robotics, Prater Motion.
Stored 4 messages of research_agent, 300 tokens, read back from JSON
Market scout: Found Alpine Grippers.
```

The research agent's code allows three companies, its stored configuration lowers that to two; the stored agent has no
settings, so its tool runs with the code defaults.

## Requirements

- The .NET 10 SDK and C# 14. `AddAgentic()` is an
  [interceptor](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12#interceptors); the package opts your
  project into the `Agentic.Generated` namespace through `buildTransitive/Agentic.props`.
- Call `services.AddAgentic()` directly. A call that is not intercepted (through a delegate, say) throws and says
  what to do.

## Documentation

| Document | Holds |
|---|---|
| [docs/architecture.md](docs/architecture.md) | How the generator, the runtime and the stores fit together. |
| [docs/diagnostics.md](docs/diagnostics.md) | Every diagnostic, AGT001 to AGT016, with its cause and fix. |
| [docs/decisions.md](docs/decisions.md) | Why the library is shaped the way it is, with the alternatives. |
| [docs/renaming.md](docs/renaming.md) | Where each placeholder name lives and how to rename it. |
| [docs/status.md](docs/status.md) | What is done, which test covers which behavior, the known gaps and next steps. |
| [AGENTS.md](AGENTS.md) | How to build, test and contribute. |

## License

[MIT](LICENSE)
