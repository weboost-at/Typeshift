using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Stores.EfCore;
using Typeshift.Samples.Tickets;
using Typeshift.Samples.Web;

namespace Typeshift.Samples.Scenarios;

// Serves the chat endpoints on a local port and calls them as a chat UI would: a message, another customer's chat,
// a decision loaded again and answered by its id, and a streamed answer
internal static class ChatEndpoint
{
    public static async Task RunAsync(IChatClient client, string database)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        Registration.Register(builder.Services, client, client, database);
        builder.Services.AddSingleton<IChats, SampleChats>();

        await using var app = builder.Build();

        var contexts = app.Services.GetRequiredService<IDbContextFactory<TypeshiftDb>>();

        await using (var db = await contexts.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        app.MapChats();
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
        if (!events.Contains("event: decision", StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected a decision event: {events}");

        var streamed = JsonSerializer.Deserialize<Decision>(
            events.Split('\n').Single(line => line.StartsWith("data: {", StringComparison.Ordinal))[6..],
            JsonSerializerOptions.Web)!;

        var always = new { decisionId = streamed.Id, approved = true, forConversation = true };
        await PostAsync<ChatReply>(http, "/chats/43/decisions", always);

        var again = await PostAsync<ChatReply>(http, "/chats/43/messages", new ChatInput("Close ticket 1234 again."));
        if (again.Decisions.Count > 0) throw new InvalidOperationException("Expected the standing approval.");

        var text = await http.PostAsJsonAsync("/chats/41/messages/stream", new ChatInput("Where is my refund?"));
        var answer = await text.Content.ReadAsStringAsync();
        var textEvent = $"event: text\ndata: {JsonSerializer.Serialize(CannedChatClient.Answer)}";
        if (!answer.Contains(textEvent, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected the answer as a JSON string: {answer}");

        await app.StopAsync();
    }

    private static async Task<T> PostAsync<T>(HttpClient http, string path, object body)
    {
        var response = await http.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
