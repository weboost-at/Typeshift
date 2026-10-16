# Support desk demo

Four agents and what connects them: triage hands the conversation to billing or support, support asks a help
center sub-agent and an MCP status server, billing refunds behind the host's approval, every run learns the customer
from a context provider, a run middleware answers blocked topics itself, each customer has settings per agent under
one settings id, and a report agent summarizes the conversation as typed JSON.

```bash
# offline, on canned models
dotnet run --project demos/Typeshift.Demos.Support -- --trace

# on a local LM Studio model (a model that calls tools well; the report agent uses a JSON schema)
dotnet run --project demos/Typeshift.Demos.Support -- lmstudio/qwen/qwen3.8-27b
```

Conversations and settings live under `.typeshift/` in the current directory.
