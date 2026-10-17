# Multi-provider demo

An app whose users define model providers at runtime, in `providers.json`, and pick a model per user. The agent's
code names no provider: the user's choice is a stored setting, and `RoutingChatClientProvider` maps it to a client.

```bash
# offline, on the canned provider
dotnet run --project demos/Typeshift.Demos.MultiProvider

# a local LM Studio server, any model it has loaded
dotnet run --project demos/Typeshift.Demos.MultiProvider -- lmstudio/qwen/qwen3.8-27b --trace

# OpenAI or Anthropic, keys from the environment
OPENAI_API_KEY=... dotnet run --project demos/Typeshift.Demos.MultiProvider -- openai/gpt-4o-mini
ANTHROPIC_API_KEY=... dotnet run --project demos/Typeshift.Demos.MultiProvider -- anthropic/claude-sonnet-4-5

# what a settings UI could list, and adding a provider at runtime
dotnet run --project demos/Typeshift.Demos.MultiProvider -- --list lmstudio
dotnet run --project demos/Typeshift.Demos.MultiProvider -- --add groq openai https://api.groq.com/openai/v1 '${GROQ_API_KEY}'
```

The `CLUNKY n` comments in the code mark where the current library makes this harder than it should be.
