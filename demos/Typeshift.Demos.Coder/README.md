# Coder demo

A coding agent over a workspace, with what a long-running agent needs: plan mode and the host's approval of the plan,
questions to the user, approval rules for the shell, tool middleware, a background explorer agent, a context
provider, a conversation on disk that survives a restart, steering while it works, and the live list of runs.

`CoderAgent` also sets a limit of 24,000 input tokens, from which Typeshift compacts the history. The offline run
below stays far under it and shows no compaction. To see one, give the agent a task long enough to reach the limit,
on a real model.

```bash
# offline, on the canned models, against the bundled sample workspace, approving everything
dotnet run --project demos/Typeshift.Demos.Coder -- --yes --trace

# interactive, on a local LM Studio model, in a workspace of your own
dotnet run --project demos/Typeshift.Demos.Coder -- ~/src/my-project lmstudio/qwen/qwen3.8-27b

# the conversations on disk, and continuing one (an interrupted run is resumed first)
dotnet run --project demos/Typeshift.Demos.Coder -- --list
dotnet run --project demos/Typeshift.Demos.Coder -- ~/src/my-project --resume <conversation id>
```

Type a task at the prompt. While the agent works, whatever you type is steered into its run before its next model
call. The plan is approved into auto mode (`a`), into the default mode that asks per tool (`d`), or rejected with
your feedback. The shell guard rejects `sudo`, `rm -rf` and the like outright and approves commands that only look.

`CoderAgent` declares `UseQuestions()`, so a real model may ask which way to go before it plans or edits. Answer
with the number of an option, several numbers where the question takes them, or your own words. The canned models
of the offline run never ask.

Conversations and settings live under `.typeshift/` in the current directory.
