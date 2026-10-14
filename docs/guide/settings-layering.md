# Settings layering

A run's settings come from four layers: the code's defaults, the agent's code, the settings stored under a settings
id, and the run's own. Each layer sets values over the one below, and a value it leaves null keeps the one below.
Tool settings, chat options and run options all resolve this way, when a run starts.

## One value, four layers

Follow the `find_companies` limit of the [getting started](./getting-started.md) agent through the layers.

**The code.** `FindCompaniesSettings` sets `Limit = 5` in its parameterless constructor. This layer sets every
member, so a tool always receives a whole record, whatever the layers above leave out.

**The agent.** `ResearchAgent` calls `ConfigureFindCompanies().WithLimit(3)`, so every run of the agent searches with
3. Its chat options, `WithTemperature(0.2f)`, and its run options belong to this layer too. The agent's code may set
any settable member, configurable or not: it is code, reviewed and compiled with the rest.

**The stored settings.** `ConfigureAsync(42, ...)` stores `Limit = 2` and `Temperature = 0.4f` under settings id 42.
Every run with that settings id now searches with 2, at 0.4. Only the configurable members exist in
`ResearchAgent.Settings`, so `IncludeInactive` cannot be stored: there is no member to hold it. What a settings id
stands for is the host's choice: a customer, a team, a user, or a settings profile of its own. Under one settings
id, each agent has its own document.

**The run.** A run that passes `FindCompanies = new() { Limit = 1 }` in its settings searches with 1, for that run
only. It leaves `Temperature` null, so the stored 0.4 holds.

## Precedence

The highest layer that sets a value wins.

| Layer | Set by | May set | Holds for |
|---|---|---|---|
| 4. The run | The `settings` argument of `RunAsync` and the other runs | Configurable members | That run |
| 3. Stored | `Settings` under the run's settings id: `ConfigureAsync`, `SaveSettingsAsync` | Configurable members | Every run with that settings id |
| 2. The agent's code | `Configure{Tool}()`, `WithChatOptions()`, `WithRunOptions()`, `GetInstructions()` | Every settable member | Every run of the agent |
| 1. The code's defaults | The settings record's parameterless constructor; the run options' defaults | Every member | Every run |

## What else to know

- **Null keeps, so no layer clears.** A layer replaces a value below it or leaves it alone; it cannot take a
  temperature the agent's code set away.
- **Records replace.** A configurable member that is itself a record or a class is replaced as a whole, never
  merged with the one below.
- **The model is a value like any other.** It stays in code unless `Configurable(o => o.ModelId)` lets the layers
  above choose it, so stored settings never pick a model the agent does not allow.
- **Instructions replace, then grow.** Stored or run `Instructions` replace those of `GetInstructions()`. Context
  providers and plan mode then append to whatever the layers resolved.
- **A conversation keeps its mode.** Between the stored settings and the run, a run with a conversation id continues
  in the mode the conversation's last run ended in. A run whose own settings name a mode switches it, and so does an
  approved plan.
- **Sub-agents and handoffs keep the settings id.** A sub-agent runs with its own settings stored under the caller's
  settings id, in the caller's mode, without the caller's run settings. An agent handed off to answers with its own
  stored settings under the same settings id; the run's settings apply only to the agent the run started on.
- **Layers resolve when a run starts.** Settings saved during a run apply from the next run on.

See [the settings record](./generated-members.md#settings) for what the stored layer holds and how it is
serialized, and [running agents](./running-agents.md) for passing a settings id and run settings.
