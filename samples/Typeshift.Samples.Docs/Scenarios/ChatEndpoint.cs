using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Stores.EfCore;
using Typeshift.Samples.Tickets;
using Typeshift.Samples.Web;

namespace Typeshift.Samples.Scenarios;

// Serves the chat endpoints and the hub on a local port and calls them as a chat UI would: a message, another
// customer's chat, a decision loaded again and answered by its id, questions answered by label and in words, a
// streamed answer that ends with its done event, one the provider drops with its error event, an AG-UI client's runs,
// a second message while the answer to the first is under way, the hub's negotiation, and a chat loaded again, one
// compaction summarized included. Then the customer's settings form, and a staff endpoint that binds the whole
// Settings record
internal static class ChatEndpoint
{
    public static async Task RunAsync(IChatClient client, string database)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // The answer the scenario drops logs an error, which it checks rather than prints
        var errors = new ErrorLog();
        builder.Logging.AddProvider(errors);
        builder.Logging.AddFilter<ConsoleLoggerProvider>(typeof(SupportAgent).FullName, LogLevel.None);

        var held = new HeldChatClient(new DroppingChatClient(client));
        Registration.Register(builder.Services, held, held, database);
        builder.Services.AddSingleton<IChats, SampleChats>();

        // begin-snippet: conversation-hub-map
        builder.Services.AddSignalR();

        await using var app = builder.Build();

        app.MapHub<ConversationHub>("/hub");
        // end-snippet

        var contexts = app.Services.GetRequiredService<IDbContextFactory<TypeshiftDb>>();

        await using (var db = await contexts.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        app.MapChats();
        app.MapSettings();

        app.MapPut("/staff/settings/{customerId:int}", (
            int customerId,
            SupportAgent.Settings settings,
            AgentHost<SupportAgent> support,
            CancellationToken ct) => support.WithSettingsId(customerId).UpdateSettingsAsync(_ => settings, ct));

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };

        var reply = await PostAsync<ChatReply>(http, "/chats/41/messages", new ChatInput("Where is my refund?"));
        if (reply.Text != CannedChatClient.Answer)
            throw new InvalidOperationException($"Unexpected answer: {reply.Text}");

        var other = await http.PostAsJsonAsync("/chats/99/messages", new ChatInput("Where is my refund?"));
        if (other.StatusCode != HttpStatusCode.NotFound)
            throw new InvalidOperationException("Expected another customer's chat to be hidden.");

        var asked = await PostAsync<ChatReply>(http, "/chats/42/messages", new ChatInput("Close ticket 1234."));
        if (asked.Decisions is not [ApprovalDecision { Tool: "close_ticket" } pending])
            throw new InvalidOperationException("Expected one decision about close_ticket.");

        var reloaded = await http.GetFromJsonAsync<Decision[]>("/chats/42/decisions");
        if (reloaded is not [{ } waiting] || waiting.Id != pending.Id)
            throw new InvalidOperationException("Expected the page to load the decision again.");

        var approved = new { decisionId = pending.Id, approved = true };
        await PostAsync<ChatReply>(http, "/chats/42/decisions", approved);

        var twice = await http.PostAsJsonAsync("/chats/42/decisions", approved);
        if (twice.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException("Expected the answer posted again to conflict.");

        var ticket = await app.Services.GetRequiredService<ITicketClient>().GetAsync(1234, default);
        if (ticket is not { Closed: true }) throw new InvalidOperationException("Expected the approved call to run.");

        var stream = await http.PostAsJsonAsync("/chats/43/messages/stream", new ChatInput("Close ticket 1234."));
        var events = await stream.Content.ReadAsStringAsync();
        if (!events.Contains("event: decision", StringComparison.Ordinal) || !Ends(events, Done))
            throw new InvalidOperationException($"Expected a decision event, then the done event: {events}");

        var streamed = JsonSerializer.Deserialize<Decision>(
            events.Split('\n').Single(line => line.StartsWith("data: {", StringComparison.Ordinal))[6..],
            JsonSerializerOptions.Web)!;

        var always = new { decisionId = streamed.Id, approved = true, forConversation = true };
        await PostAsync<ChatReply>(http, "/chats/43/decisions", always);

        var again = await PostAsync<ChatReply>(http, "/chats/43/messages", new ChatInput("Close ticket 1234 again."));
        if (again.Decisions.Count > 0) throw new InvalidOperationException("Expected the standing approval.");

        var opening = new ChatInput("Open a ticket for my dishwasher.");
        var questions = await PostAsync<ChatReply>(http, "/chats/44/messages", opening);
        if (questions.Decisions is not [QuestionDecision { Questions: [{ Options: [var first, ..] }, _] } question])
            throw new InvalidOperationException("Expected one decision with two questions.");

        var answers = new
        {
            decisionId = question.Id,
            answers = new object[] { new { selected = new[] { first.Label } }, new { text = "Since Monday." } }
        };

        var answered = await PostAsync<ChatReply>(http, "/chats/44/decisions", answers);
        if (answered.Text != CannedChatClient.Answer || answered.Decisions.Count > 0)
            throw new InvalidOperationException("Expected the chat to go on with the answers.");

        var text = await http.PostAsJsonAsync("/chats/41/messages/stream", new ChatInput("Where is my refund?"));
        var answer = await text.Content.ReadAsStringAsync();
        var textEvent = $"event: text\ndata: {JsonSerializer.Serialize(CannedChatClient.Answer)}";
        if (!answer.Contains(textEvent, StringComparison.Ordinal) || !Ends(answer, Done))
            throw new InvalidOperationException($"Expected the answer as a JSON string, then the done event: {answer}");

        var lengthy = new ChatInput(DroppingChatClient.Dropped);
        var dropping = await http.PostAsJsonAsync("/chats/47/messages/stream", lengthy);
        var dropped = await dropping.Content.ReadAsStringAsync();
        if (!dropped.Contains(textEvent, StringComparison.Ordinal) || !Ends(dropped, Error))
            throw new InvalidOperationException($"Expected the answer so far, then the error event: {dropped}");

        if (dropped.Contains(DroppingChatClient.Reason, StringComparison.Ordinal))
            throw new InvalidOperationException("Expected the error event to leave out the exception's message.");

        if (!errors.Exceptions.Any(exception => exception.ToString().Contains(DroppingChatClient.Reason)))
            throw new InvalidOperationException("Expected the endpoint to log the exception.");

        await AgUi.RunAsync(http, DroppingChatClient.Dropped, DroppingChatClient.Reason);

        var slow = http.PostAsJsonAsync("/chats/45/messages", new ChatInput(HeldChatClient.Held));
        await held.Holding.Task;

        var doubled = await http.PostAsJsonAsync("/chats/45/messages", new ChatInput("Are you there?"));
        if (doubled.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException("Expected a second message to a chat that is answering to conflict.");

        var doubledStream = await http.PostAsJsonAsync("/chats/45/messages/stream", new ChatInput("Are you there?"));
        if (doubledStream.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException("Expected a second streamed message to conflict before it streams.");

        if ((await AgUi.PostAsync(http, "Are you there?", chat: 45)).StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException("Expected an AG-UI run of a chat that is answering to conflict.");

        held.Release.SetResult();
        (await slow).EnsureSuccessStatusCode();

        var hub = await http.PostAsync("/hub/negotiate?negotiateVersion=1", content: null);
        if (hub.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Expected the hub to be mapped: {hub.StatusCode}");

        await CheckHistoryAsync(app.Services, http);

        await CheckSettingsAsync(app.Services, http);
        await app.StopAsync();
    }

    // The last event of a stream: its run ended, answered or asking, or failed after the response began
    private const string Done = "event: done\ndata: \"\"\n\n";

    private const string Error = "event: error\ndata: \"\"\n\n";

    private static bool Ends(string events, string last) => events.EndsWith(last, StringComparison.Ordinal);

    // Streams its answer to one message and then loses the connection, as a provider can while it answers
    private sealed class DroppingChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public const string Dropped = "Tell me everything about my refund.";

        public const string Reason = "The provider reset the connection.";

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }

            if (messages.Last().Text == Dropped) throw new HttpRequestException(Reason);
        }
    }

    private sealed class ErrorLog : ILoggerProvider
    {
        public ConcurrentQueue<Exception> Exceptions { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(Exceptions);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<Exception> exceptions) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (exception is not null && IsEnabled(logLevel)) exceptions.Enqueue(exception);
            }
        }
    }

    // Holds the answer to one message until the scenario lets it go, as a model that takes its time would
    private sealed class HeldChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public const string Held = "Take your time.";

        public TaskCompletionSource Holding { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (messages.Last().Text != Held) return await base.GetResponseAsync(messages, options, cancellationToken);

            Holding.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);

            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
    }

    // Chat 41 holds two answers. Chat 46 holds one, a summary that kept it, and the answer of a run after it, so the
    // page reads each line once and none of the summary
    private static async Task CheckHistoryAsync(IServiceProvider services, HttpClient http)
    {
        var lines = await http.GetFromJsonAsync<ChatLine[]>("/chats/41/messages");
        if (lines is not [{ Role: "user" }, { Role: "assistant" }, { Role: "user" }, { Role: "assistant" }])
            throw new InvalidOperationException("Expected the chat's two questions and answers.");

        var store = services.GetRequiredService<IConversationStore<int>>();
        var at = TimeProvider.System.GetUtcNow();
        var question = new ChatMessage(ChatRole.User, "Where is my refund?");
        var answer = new ChatMessage(ChatRole.Assistant, CannedChatClient.Answer);
        var summary = new ChatMessage(ChatRole.User, "A summary of the conversation so far: the refund was approved.");

        ConversationEntry[] entries =
        [
            new() { Agent = "support_agent", Messages = [question], At = at, Sequence = 1 },
            new() { Agent = "support_agent", Messages = [answer], At = at, Sequence = 2 },
            new() { Agent = "support_agent", At = at, Sequence = 3, EndsRun = true },
            new() { Agent = "support_agent", Messages = [summary, answer], At = at, Sequence = 4, IsSummary = true }
        ];

        foreach (var entry in entries)
        {
            await store.AppendConversationAsync(46, entry, default);
        }

        await PostAsync<ChatReply>(http, "/chats/46/messages", new ChatInput("When will it arrive?"));

        var compactedLines = await http.GetFromJsonAsync<ChatLine[]>("/chats/46/messages");
        if (compactedLines?.Select(line => line.Text).ToArray() is not
            ["Where is my refund?", CannedChatClient.Answer, "When will it arrive?", CannedChatClient.Answer])
            throw new InvalidOperationException("Expected each line of a compacted chat once, and no summary.");
    }

    private static async Task CheckSettingsAsync(IServiceProvider services, HttpClient http)
    {
        var form = await http.PutAsJsonAsync("/settings", new SupportPreferences("de"));
        form.EnsureSuccessStatusCode();

        await using var scope = services.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var customer = scope.ServiceProvider.GetRequiredService<ICurrentCustomer>();

        var stored = await support.WithSettingsId(customer.Id).LoadSettingsAsync();
        if (stored.Data.SearchArticles.Language != "de" || stored.Data.Model is not null)
            throw new InvalidOperationException("Expected the form to set the article language alone.");

        var miscased = await http.PutAsync("/staff/settings/8", Json("""{"Temperature":0.5}"""));
        if (miscased.StatusCode != HttpStatusCode.BadRequest)
            throw new InvalidOperationException("Expected a member in another case to be refused.");

        if ((await support.WithSettingsId(8).LoadSettingsAsync()).Version != 0)
            throw new InvalidOperationException("Expected a refused body to store nothing.");

        var staff = await http.PutAsync("/staff/settings/8", Json("""{"temperature":0.5}"""));
        staff.EnsureSuccessStatusCode();

        if ((await support.WithSettingsId(8).LoadSettingsAsync()).Data.Temperature != 0.5f)
            throw new InvalidOperationException("Expected the staff endpoint to store the settings it was sent.");
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<T> PostAsync<T>(HttpClient http, string path, object body)
    {
        var response = await http.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
