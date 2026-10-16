---
layout: home

hero:
  name: Typeshift
  text: Agents and tools, defined in code
  tagline: Declare a tool in one line and an agent in a few. Typeshift generates the plumbing and catches mistakes at compile time.
  actions:
    - theme: brand
      text: Get started
      link: /guide/getting-started
    - theme: alt
      text: How it compares
      link: /guide/comparison
    - theme: alt
      text: View on GitHub
      link: https://github.com/weboost-at/Typeshift

features:
  - title: Tools from static methods
    details: Each tool becomes a generated AIFunction, with a JSON schema from its parameters and XML docs. No reflection binds the model's arguments.
    link: /guide/toolsets
    linkText: Declare a toolset
  - title: Typed, layered settings
    details: Code defaults, the agent's code, stored settings and the run layer on each other. Only what the code marks configurable can change.
    link: /guide/settings-and-stores#settings-layering
    linkText: Follow a value through the layers
  - title: A settings contract per agent
    details: Every agent gets a generated Settings record with exactly its configurable values, ready to back a settings form or an API.
    link: /guide/defining-agents
    linkText: Define an agent
  - title: One store, your ids
    details: Implement ITypeshiftStore once, with the id type you already use. Without a store, everything runs from code.
    link: /guide/settings-and-stores
    linkText: Store settings
  - title: Conversations that survive a crash
    details: Each step of a run is stored the moment it exists, so a run resumes where it stopped. Steer a run while it works, or watch it live.
    link: /guide/conversations
    linkText: Keep a conversation
  - title: Approval, plan and auto modes
    details: Tools that must not run unasked wait for the host. Rules decide what they can, plan mode only reads, auto mode runs what the rules allow.
    link: /guide/tool-approval
    linkText: Approve tool calls
  - title: Long runs and many agents
    details: Compaction keeps a long run within its context. Sub-agents work in the foreground or the background, and a handoff passes the conversation on.
    link: /guide/long-running-agents
    linkText: Run for hours
  - title: Built-in toolsets
    details: To-dos, files within a workspace, the shell and the web, declared like your own. MCP servers plug in through the client of your choice.
    link: /guide/built-in-toolsets
    linkText: Use the built-in tools
  - title: Mistakes are compile errors
    details: Including a tool of the wrong toolset, or configuring a tool the agent does not offer, does not compile. The analyzer's diagnostics cover the rest.
    link: /guide/diagnostics
    linkText: See the diagnostics
---
