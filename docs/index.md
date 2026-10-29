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
    details: Inject the agent's host into a controller or an endpoint, or resolve it in a background job, and its tools use that request's or job's services.
    link: /guide/using-an-agent
    linkText: Use an agent
  - title: Catch mistakes at compile time
    details: Configure a tool the agent does not offer, and the build fails. The analyzer reports the rest, each with how to fix it.
    link: /guide/diagnostics
    linkText: See the diagnostics
  - title: Store settings per customer
    details: Each agent gets a typed Settings record of what you mark configurable. Store it in your own database, under your own ids.
    link: /guide/settings-and-stores
    linkText: Store settings
  - title: Keep conversations that survive a crash
    details: Every step is stored as it happens, so a run resumes where it stopped. Steer a run while it works, or watch it live.
    link: /guide/conversations
    linkText: Keep a conversation
  - title: Approve, plan and delegate
    details: Tool approval, plan mode, sub-agents, compaction, MCP servers, and built-in tools for files, the shell and the web.
    link: /guide/tool-approval
    linkText: Approve tool calls
  - title: Publish with Native AOT
    details: The generator writes every path a run takes, so nothing reflects, and the analyzer reports a tool type that lacks JSON metadata.
    link: /guide/production#native-aot
    linkText: Publish with AOT
---
