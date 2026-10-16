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
      text: View on GitHub
      link: https://github.com/weboost-at/Typeshift

features:
  - title: A tool is a method
    details: Write a static method and add it with one line. Its XML docs tell the model what it does.
    link: /guide/toolsets
    linkText: Write a toolset
  - title: An agent is a constructor
    details: A few chained calls pick the model, the instructions, the tools and their settings.
    link: /guide/defining-agents
    linkText: Write an agent
  - title: Mistakes do not compile
    details: Configure a tool the agent does not offer, and the build fails. The analyzer catches the rest.
    link: /guide/diagnostics
    linkText: See the diagnostics
  - title: A settings contract per agent
    details: Each agent gets a typed Settings record of what you mark configurable. Store it per customer, in your database.
    link: /guide/settings-and-stores
    linkText: Store settings
  - title: Conversations that survive a crash
    details: Every step is stored as it happens, so a run resumes where it stopped. Steer it or watch it live.
    link: /guide/conversations
    linkText: Keep a conversation
  - title: Ready for real agents
    details: Tool approval, plan mode, sub-agents, compaction, MCP servers, and built-in tools for files, the shell and the web.
    link: /guide/built-in-toolsets
    linkText: Use the built-in tools
---
