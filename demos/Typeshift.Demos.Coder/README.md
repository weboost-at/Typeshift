# Coder demo

A coding agent over a workspace, with what a long-running agent needs: plan mode and the host's approval of the plan,
questions to the user, approval rules for the shell, tool middleware, a background explorer agent, a context
provider, a conversation on disk that survives a restart, steering while it works, and the live list of runs.

`CoderAgent` also sets a limit of 24,000 input tokens, from which Typeshift compacts the history. The offline run
below stays far under it and shows no compaction. To see one, give the agent a task long enough to reach the limit,
on a real model.

```bash
# offline, on the canned models, in a copy of the sample workspace, approving everything
dotnet run --project demos/Typeshift.Demos.Coder -- --yes --trace

# interactive, on a local LM Studio model, in a workspace of your own
dotnet run --project demos/Typeshift.Demos.Coder -- ~/src/my-project --model lmstudio/qwen/qwen3.8-27b

# the conversations on disk, and continuing one (an interrupted run is resumed first)
dotnet run --project demos/Typeshift.Demos.Coder -- --list
dotnet run --project demos/Typeshift.Demos.Coder -- ~/src/my-project --resume <conversation id>
```

The demo runs commands with `/bin/sh`, and the sample workspace's tests are shell scripts, so it runs on Linux,
macOS or in WSL, not on Windows itself. Without a workspace it works in a fresh copy of the sample workspace, one per
conversation under `.typeshift/workspaces/`, so every offline run starts from the failing tests and `--resume <id>`
finds the copy of its conversation. Each run ends by printing the arguments that continue it.

Type a task at the prompt. While the agent works, whatever you type is steered into its run before its next model
call. An empty line, Ctrl+C or the end of the input stops the session; what the run stored stays, and a run cut off
resumes. The plan is approved into auto mode (`a`), into the default mode that asks per tool (`d`), or rejected with
your feedback.

The shell guard, `ShellGuard`, approves only `pwd` and `ls` of a folder in the workspace, which run nothing the
workspace holds; git is not among them, since a repository's config, which the file tools may write, names commands
git runs. In the default mode every other command asks at the console, and so does every call of `write_file`,
`edit_file` and `delete_file`, so the model cannot rewrite `.git/config` or a script unseen before a command runs
it. In auto mode nobody is asked, so the guard rejects every command but those and the sample workspace's
`sh test.sh`, and the model reads why. There the file tools write unasked, and the model can change `test.sh`
before it runs it: approve a plan into auto mode only for a workspace you would let it run code in.

`CoderAgent` declares `UseQuestions()`, so a real model may ask which way to go before it plans or edits. Answer
with the number of an option, several numbers where the question takes them, or your own words. The canned models
of the offline run never ask.

Conversations, settings and the providers live under `.typeshift/` in the current directory.
