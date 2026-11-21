using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift;
using Typeshift.Demos.Coder;
using Typeshift.Demos.Coder.Agents;
using Typeshift.Demos.Coder.Hosting;
using Typeshift.Demos.Hosting;
using Typeshift.Demos.Providers;

// A coding agent over a workspace: it plans in plan mode, asks the console to approve the plan and the commands
// that need it, edits files, runs the tests, works with a background explorer, compacts a long run, and keeps the
// conversation on disk so a run cut off can be resumed. Type while it works to steer it. Without a workspace it works
// in a copy of the sample workspace, one per conversation under .typeshift/workspaces/.
//
//   dotnet run --project demos/Typeshift.Demos.Coder -- [workspace] [--model provider/model] [--yes] [--trace] [--once]
//   dotnet run --project demos/Typeshift.Demos.Coder -- --list
//   dotnet run --project demos/Typeshift.Demos.Coder -- [workspace] --resume <conversation id>
return await Demo.RunAsync(async () =>
{
    var catalog = Demo.Catalog();
    using var spans = Demo.Flag(args, "--trace") ? new ConsoleSpans() : null;

    // The options that take a value, so that value is never taken for the workspace
    string[] valued = ["--model", "--resume"];

    var given = args
        .Where((argument, at) => !argument.StartsWith("--", StringComparison.Ordinal)
            && (at == 0 || !valued.Contains(args[at - 1])))
        .FirstOrDefault();

    var model = Demo.Option(args, "--model");
    var resume = Demo.Flag(args, "--resume") ? ConversationId(Demo.Option(args, "--resume")) : (Guid?)null;
    var approveAll = Demo.Flag(args, "--yes");

    var conversationId = resume ?? Guid.CreateVersion7();
    var workspace = given is null
        ? Path.Combine(Demo.DataDirectory, "workspaces", conversationId.ToString())
        : Path.GetFullPath(given);

    var canned = new CannedModels
    {
        ["coder"] = ScriptedCoder.Coder(),
        ["explorer"] = ScriptedCoder.Explorer()
    };

    var services = new ServiceCollection();
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton(new WorkspaceOptions(workspace));
    services.AddSingleton<SafeWebFetch>();
    services.AddDemoServices(catalog, canned);

    services.AddTypeshift()
        .WithStore(Demo.Store())
        .WithApprovalRule<ShellGuard>()
        .WithToolMiddleware<AuditToolCalls>();

    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    });

    var store = provider.GetRequiredService<JsonFileStore>();
    var tracker = provider.GetRequiredService<ConversationTracker<Guid>>();

    if (Demo.Flag(args, "--list"))
    {
        Demo.Heading("Conversations on disk");

        foreach (var id in store.Conversations())
        {
            var entries = await store.LoadConversationAsync(id, CancellationToken.None);
            var state = entries.Count == 0 ? "empty" : entries[^1].EndsRun ? "ended" : "interrupted";
            Demo.Note($"{id} {entries.Count,4} entries, {state}, last by {entries.LastOrDefault()?.Agent}");
        }

        return 0;
    }

    // The shell tool runs every command with /bin/sh, and the sample workspace's tests are shell scripts
    if (OperatingSystem.IsWindows())
        throw new DemoException("The Coder demo runs commands with /bin/sh: run it on Linux, macOS or in WSL.");

    if (resume is { } resumed && !store.Conversations().Contains(resumed))
        throw new DemoException($"No conversation {resumed} is stored here; --list shows the ones that are.");

    if (given is null && resume is null)
        Copy(Path.Combine(AppContext.BaseDirectory, "sample-workspace"), workspace);

    if (!Directory.Exists(workspace))
    {
        throw new DemoException(given is null
            ? $"Conversation {conversationId} has no copy of the sample workspace: name its workspace before --resume."
            : $"The workspace '{workspace}' does not exist.");
    }

    await using var scope = provider.CreateAsyncScope();

    // The settings of this workspace: the model the user picked, and limits a host might raise per customer
    var settingsId = Guid.Parse("00000000-0000-0000-0000-000000000007");
    var coder = scope.ServiceProvider.GetRequiredService<AgentHost<CoderAgent>>().WithSettingsId(settingsId);
    var coding = ModelRef.Parse(model ?? "canned/coder");
    var exploring = ModelRef.Parse(model ?? "canned/explorer");

    await coder.UpdateSettingsAsync(settings => settings with
    {
        Provider = coding.Provider,
        Model = coding.Model,
        MaxToolIterations = 150
    });

    // The explorer is a separate agent with settings of its own under the same id
    await scope.ServiceProvider.GetRequiredService<AgentHost<ExplorerAgent>>()
        .WithSettingsId(settingsId)
        .UpdateSettingsAsync(settings => settings with { Provider = exploring.Provider, Model = exploring.Model });

    var chat = coder.WithConversationId(conversationId);

    Demo.Heading($"Workspace {workspace}");
    Demo.Note($"model {coding}, conversation {conversationId}");
    Demo.Note("type a task; while the agent works, what you type steers it; an empty line or Ctrl+C quits");

    // One thread reads the console for the whole session: a line is the next task when the agent is idle, and
    // steering input while it works
    var input = Channel.CreateUnbounded<string>();

    new Thread(() =>
    {
        while (Console.ReadLine() is { } line) input.Writer.TryWrite(line);
        input.Writer.TryComplete();
    })
    {
        IsBackground = true
    }.Start();

    using var session = new CancellationTokenSource();
    Console.CancelKeyPress += (_, pressed) =>
    {
        pressed.Cancel = true;
        session.Cancel();
    };

    // Runs starting and ending, as a UI's list of running conversations would hear them
    _ = Task.Run(async () =>
    {
        try
        {
            await foreach (var run in tracker.WatchRunsAsync(session.Token))
            {
                Console.Error.WriteLine(
                    $"  [runs] {run.Agent} on {run.Conversation} {(run.Ended ? "ended" : "started")}");
            }
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested)
        {
            Console.Error.WriteLine("  [runs] stopped watching");
        }
    });

    try
    {
        ChatMessage? next = null;
        IReadOnlyList<DecisionAnswer> answers = [];

        if (resume is not null)
        {
            var entries = await store.LoadConversationAsync(conversationId, session.Token);
            var waiting = await chat.GetDecisionsAsync(session.Token);

            // A run cut off goes on; one that waited for an answer returns what it asked, without a model call
            if (waiting.Open.Count > 0 || waiting.CanResume || (entries.Count > 0 && !entries[^1].EndsRun))
            {
                Demo.Heading("Resuming the interrupted run");
                var response = await RunAsync(chat.ResumeStreamingAsync(ct: session.Token));
                answers = ConsoleApprover.Answers(response, ReadLine, approveAll);
            }
        }

        if (answers.Count == 0)
        {
            Console.Write("\ntask> ");
            var task = Demo.Flag(args, "--once") || approveAll ? "Make the tests in test.sh pass." : ReadLine();
            if (task.Length == 0) return 0;
            next = new ChatMessage(ChatRole.User, task);
        }

        while (next is not null || answers.Count > 0)
        {
            ChatResponse response;

            try
            {
                response = await RunAsync(answers.Count > 0
                    ? chat.AnswerStreamingAsync(answers, ct: session.Token)
                    : chat.RunStreamingAsync(next!, ct: session.Token));
            }
            catch (DecisionClosedException)
            {
                // Another process answered first, or a newer message passed the decision by
                Demo.Note("That decision is closed: the conversation went on without this answer.");
                response = new ChatResponse();
            }

            answers = ConsoleApprover.Answers(response, ReadLine, approveAll);
            next = null;

            if (answers.Count == 0 && !Demo.Flag(args, "--once") && !approveAll)
            {
                Console.Write("\ntask> ");
                var task = ReadLine();
                next = task.Length == 0 ? null : new ChatMessage(ChatRole.User, task);
            }
        }
    }
    catch (OperationCanceledException) when (session.IsCancellationRequested)
    {
        // Ctrl+C or the end of the input: what the run stored stays, and a run cut off resumes
        Console.WriteLine();
        Demo.Note("stopped");
    }

    var stored = await store.LoadConversationAsync(conversationId, CancellationToken.None);
    if (stored.Count == 0) return 0;

    Demo.Heading("Stored");
    var mode = (await chat.GetDecisionsAsync(CancellationToken.None)).Mode?.ToString() ?? "the agent's own";
    var tokens = stored.Sum(entry => entry.Usage?.TotalTokenCount ?? 0);

    Demo.Note($"{stored.Count} entries, {tokens} tokens, in {mode} mode");
    Demo.Note($"resume or continue with: {(given is null ? "" : $"\"{workspace}\" ")}--resume {conversationId}");

    return 0;

    // Streams a run to the console while a second reader steers it with whatever the user types
    async Task<ChatResponse> RunAsync(IAsyncEnumerable<ChatResponseUpdate> run)
    {
        using var running = CancellationTokenSource.CreateLinkedTokenSource(session.Token);

        var steering = Task.Run(async () =>
        {
            await foreach (var line in Demo.LinesUntilAsync(input.Reader, running.Token))
            {
                try
                {
                    Console.Error.WriteLine(tracker.TrySteer(conversationId, line)
                        ? $"  [steered] {line}"
                        : $"  [not steered, the run is ending] {line}");
                }
                catch (ConversationBusyException)
                {
                    Console.Error.WriteLine($"  [not steered, the run holds as much input as it takes] {line}");
                }
            }
        });

        var updates = new List<ChatResponseUpdate>();

        try
        {
            await foreach (var update in run.WithCancellation(session.Token))
            {
                updates.Add(update);
                Print(update);
            }
        }
        finally
        {
            await running.CancelAsync();
            await steering;
        }

        Console.WriteLine();
        return updates.ToChatResponse();
    }

    // The next line the user typed; the end of the input ends the session, as Ctrl+C does
    string ReadLine()
    {
        try
        {
            return input.Reader.ReadAsync(session.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
            session.Cancel();
            throw new OperationCanceledException(session.Token);
        }
    }
});

static Guid ConversationId(string? id) =>
    Guid.TryParse(id, out var parsed)
        ? parsed
        : throw new DemoException($"--resume takes a conversation id, not '{id}'; --list shows the stored ones.");

// The sample workspace, copied whole, so the agent's edits never reach the copy the build put beside the demo
static void Copy(string from, string to)
{
    foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
    {
        var copy = Path.Combine(to, Path.GetRelativePath(from, file));
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(file, copy);
    }
}

static void Print(ChatResponseUpdate update)
{
    foreach (var content in update.Contents)
    {
        switch (content)
        {
            case TextContent text:
                Console.Write(text.Text);
                break;

            case FunctionCallContent call:
                Console.WriteLine($"\n   [call] {call.Name}({Demo.Arguments(call)})");
                break;

            case FunctionResultContent result:
                Console.WriteLine($"   [result] {Demo.Shorten(result.Result?.ToString() ?? "", 120)}");
                break;

            case DecisionRequestContent { Decision: ApprovalDecision asked }:
                Console.WriteLine($"\n   [approval needed] {asked.Tool}");
                break;

            case DecisionRequestContent { Decision: QuestionDecision asked }:
                Console.WriteLine($"\n   [the agent asks] {asked.Questions.Count} question(s)");
                break;
        }
    }
}
