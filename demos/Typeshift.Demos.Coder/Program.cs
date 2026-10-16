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
// conversation on disk so a run cut off can be resumed. Type while it works to steer it.
//
//   dotnet run --project demos/Typeshift.Demos.Coder -- [workspace] [provider/model] [--yes] [--trace] [--once]
//   dotnet run --project demos/Typeshift.Demos.Coder -- --list
//   dotnet run --project demos/Typeshift.Demos.Coder -- [workspace] --resume <conversation id>
var catalog = Demo.Catalog();
using var spans = Demo.Flag(args, "--trace") ? new ConsoleSpans() : null;

var positional = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToList();
var workspace = Path.GetFullPath(positional.FirstOrDefault(argument => !argument.Contains('/') || Directory.Exists(argument))
    ?? Path.Combine(AppContext.BaseDirectory, "sample-workspace"));
var model = positional.FirstOrDefault(argument => argument.Contains('/') && !Directory.Exists(argument));
var approveAll = Demo.Flag(args, "--yes");

var canned = new CannedModels
{
    ["coder"] = ScriptedCoder.Coder(),
    ["explorer"] = ScriptedCoder.Explorer()
};

var services = new ServiceCollection();
services.AddSingleton(TimeProvider.System);
services.AddSingleton(new WorkspaceOptions(workspace));
services.AddDemoServices(catalog, canned);

services.AddTypeshift()
    .WithChatClientProvider<RoutingChatClientProvider>()
    .WithRealtimeTracking()
    .WithFileSystemRoot<WorkspaceRoot>()
    .WithWebFetch<SafeWebFetch>()
    .WithApprovalRule<ShellGuard>()
    .WithToolMiddleware<AuditToolCalls>();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

var store = provider.GetRequiredService<JsonFileStore>();
var tracker = provider.GetRequiredService<ConversationTracker>();

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

await using var scope = provider.CreateAsyncScope();
var coder = scope.ServiceProvider.GetRequiredService<AgentHost<CoderAgent>>();

// The settings of this workspace: the model the user picked, and limits a host might raise per customer
var settingsId = Guid.Parse("00000000-0000-0000-0000-000000000007");

await coder.ConfigureAsync(settingsId, settings => settings with
{
    Model = model ?? "canned/coder",
    MaxToolIterations = 150
});

// The explorer is a separate agent with settings of its own under the same id
await scope.ServiceProvider.GetRequiredService<AgentHost<ExplorerAgent>>()
    .ConfigureAsync(settingsId, settings => settings with { Model = model ?? "canned/explorer" });

var conversationId = Demo.Option(args, "--resume") is { } resume ? Guid.Parse(resume) : Guid.CreateVersion7();

Demo.Heading($"Workspace {workspace}");
Demo.Note($"model {(await coder.LoadSettingsAsync(settingsId)).Data.Model}, conversation {conversationId}");
Demo.Note("type a task; while the agent works, what you type steers it; an empty line quits");

// One thread reads the console for the whole session: a line is the next task when the agent is idle, and steering
// input while it works
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
            Console.Error.WriteLine($"  [runs] {run.Agent} on {run.Conversation} {(run.Ended ? "ended" : "started")}");
    }
    catch (OperationCanceledException) when (session.IsCancellationRequested)
    {
        Console.Error.WriteLine("  [runs] stopped watching");
    }
});

ChatMessage? next = null;

if (Demo.Option(args, "--resume") is not null)
{
    var entries = await store.LoadConversationAsync(conversationId, session.Token);

    if (entries.Count > 0 && !entries[^1].EndsRun)
    {
        Demo.Heading("Resuming the interrupted run");
        var resumed = await RunAsync(coder.ResumeStreamingAsync(conversationId, settingsId, ct: session.Token));
        next = ConsoleApprover.Answer(resumed, ReadLine, approveAll);
    }
}

if (next is null)
{
    Console.Write("\ntask> ");
    var task = Demo.Flag(args, "--once") || approveAll ? "Make the tests in test.sh pass." : ReadLine();
    if (task.Length == 0) return 0;
    next = new ChatMessage(ChatRole.User, task);
}

while (next is not null && !session.IsCancellationRequested)
{
    var response = await RunAsync(coder.RunStreamingAsync(next, settingsId, conversationId, ct: session.Token));
    next = ConsoleApprover.Answer(response, ReadLine, approveAll);

    if (next is null && !Demo.Flag(args, "--once") && !approveAll)
    {
        Console.Write("\ntask> ");
        var task = ReadLine();
        next = task.Length == 0 ? null : new ChatMessage(ChatRole.User, task);
    }
}

Demo.Heading("Stored");
var stored = await store.LoadConversationAsync(conversationId, CancellationToken.None);
Demo.Note($"{stored.Count} entries, {stored.Sum(entry => entry.Usage?.TotalTokenCount ?? 0)} tokens, ended in {stored[^1].Mode} mode");
Demo.Note($"resume or continue with: --resume {conversationId}");

return 0;

// Streams a run to the console while a second reader steers it with whatever the user types
async Task<ChatResponse> RunAsync(IAsyncEnumerable<ChatResponseUpdate> run)
{
    using var running = CancellationTokenSource.CreateLinkedTokenSource(session.Token);

    var steering = Task.Run(async () =>
    {
        await foreach (var line in Demo.LinesUntilAsync(input.Reader, running.Token))
        {
            Console.Error.WriteLine(tracker.TrySteer(conversationId, line)
                ? $"  [steered] {line}"
                : $"  [not steered, the run is ending] {line}");
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

string ReadLine() => input.Reader.TryRead(out var line) ? line : input.Reader.ReadAsync(session.Token).AsTask().GetAwaiter().GetResult();

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
                Console.WriteLine($"   [result] {Shorten(result.Result?.ToString() ?? "")}");
                break;

            case ToolApprovalRequestContent { ToolCall: FunctionCallContent asked }:
                Console.WriteLine($"\n   [approval needed] {asked.Name}");
                break;
        }
    }
}

static string Shorten(string text) =>
    text.ReplaceLineEndings(" ") is var line && line.Length <= 120 ? line : text.ReplaceLineEndings(" ")[..120] + "…";
