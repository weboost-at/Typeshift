---
layout: home

hero:
  name: Typeshift
  text: Agents and tools, defined in code
  tagline: Build AI agents for .NET with just a few lines of code, and let the compiler catch your mistakes.
  actions:
    - theme: brand
      text: Get started
      link: /guide/getting-started
    - theme: alt
      text: What is Typeshift?
      link: /guide/what-is-typeshift
    - theme: alt
      text: View on GitHub
      link: https://github.com/weboost-at/Typeshift

features:
  - title: Design your toolsets
    details: Group your tools as you like, by feature, domain or permission level. A tool is a static method you add with one line, and its XML docs tell the model what it does.
    link: /guide/toolsets
    linkText: Write a toolset
  - title: Create an agent
    details: A few chained calls in the agent's constructor pick the model, the instructions, the tools and their settings.
    link: /guide/defining-agents
    linkText: Write an agent
  - title: Use an agent in your app
    details: Inject the agent's host into a controller or an endpoint, or take one from the runtime in a background job, and its tools use that request's or job's services.
    link: /guide/using-an-agent
    linkText: Use an agent
  - title: Catch mistakes at compile time
    details: Configure a tool the agent does not offer, and the build fails. The analyzer reports the rest, each with how to fix it.
    link: /guide/how-typeshift-reads-your-code#mistakes-the-build-catches
    linkText: See what the build catches
  - title: Store settings per customer
    details: Each agent gets a typed Settings record of what you mark configurable. Store it in your own database, under your own ids.
    link: /guide/settings-and-stores
    linkText: Store settings
  - title: Keep conversations that survive a crash
    details: Every step is stored as it happens, so a run resumes where it stopped. Steer a run while it works, or watch it live.
    link: /guide/conversations
    linkText: Keep a conversation
  - title: Approve tool calls and plan first
    details: Pause a run until your app approves a tool call, and let the model plan before it changes anything.
    link: /guide/tool-approval
    linkText: Approve tool calls
  - title: Delegate to other agents
    details: Let an agent call another agent as a sub-agent, or hand the conversation to it.
    link: /guide/multi-agent
    linkText: Add a sub-agent
  - title: Serve a chat UI
    details: Run an agent from ASP.NET Core endpoints, and stream its answer to a chat UI as server-sent events.
    link: /guide/chat-endpoint
    linkText: Serve a chat endpoint
  - title: Publish with Native AOT
    details: The generator writes the code every run takes, so nothing reflects over your types, and the build names each tool type that still needs JSON metadata.
    link: /guide/native-aot
    linkText: Publish with AOT
---

## Quick start

```bash
dotnet add package Typeshift --prerelease
```

An agent picks its model, its instructions and its tools in its constructor:

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>
```cs
internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches the companies of a city.");
        WithInstructions("Find companies with the find_companies tool and list them.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini")
            .WithTemperature(0.2f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<CompanyToolset>();

        ConfigureFindCompanies()
            .WithLimit(3);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/ResearchAgent.cs#L3-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A console app or a script creates the runtime with your provider's chat client and takes the agent's host from it:

<!-- snippet: typeshift-runtime -->
<a id='snippet-typeshift-runtime'></a>
```cs
await using var runtime = TypeshiftRuntime.Create(client);
await using var research = runtime.Agent<ResearchAgent>();

var answer = await research.RunAsync("Vienna");

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/NoContainer.cs#L11-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-typeshift-runtime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[Getting started](./guide/getting-started.md) writes the toolset this agent offers and explains every line.
