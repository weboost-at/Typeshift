---
layout: home

hero:
  name: Agentic
  text: Agents and tools, defined in code
  tagline: A .NET 10 library on Microsoft.Extensions.AI. A source generator turns your toolsets and agents into reflection-free functions, typed settings and one registration call, ready for Native AOT.
  actions:
    - theme: brand
      text: Get started
      link: /guide/getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/weboost-at/Agentic

features:
  - title: Tools from static methods
    details: Each tool becomes a generated AIFunction with a JSON schema from its parameters and XML docs. No reflection binds the model's arguments.
  - title: Typed, layered settings
    details: Code defaults, stored defaults, the agent's code, its stored settings and a single run layer on each other. Only what the code marks configurable can change.
  - title: A settings contract per agent
    details: Every agent gets a generated Settings record with exactly its configurable values, ready to back a settings UI or an API.
  - title: One store, your ids
    details: Implement IAgenticStore once, with the id type you already use. Without a store everything runs from code.
  - title: Conversation tracking
    details: Every exchange with the model is tracked live and stored as a session of its conversation, streaming included.
  - title: Mistakes are compile errors
    details: Selecting a tool of the wrong toolset or passing settings no tool reads does not compile; fourteen diagnostics cover the rest.
---
