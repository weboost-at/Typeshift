using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AGUI.Abstractions;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Web;

namespace Typeshift.Samples.Scenarios;

// Calls the AG-UI endpoint as an AG-UI client would: an answer, another customer's chat, a call that needs approval
// and ends the run with an interrupt, the interrupt answered on the next request and answered again, questions
// answered the same way, and an answer the provider drops
internal static class AgUi
{
    public static async Task RunAsync(HttpClient http, string dropped, string reason)
    {
        var answered = await RunAsync(http, Input("Where is my refund?"));
        if (!answered.Any(item => Is(item, "TEXT_MESSAGE_CONTENT") && Text(item, "delta") == CannedChatClient.Answer)
            || !Finished(answered, "success"))
            throw new InvalidOperationException("Expected the answer's text, then a run that finished.");

        var other = await PostAsync(http, "Where is my refund?", chat: 99);
        if (other.StatusCode != HttpStatusCode.NotFound)
            throw new InvalidOperationException("Expected another customer's chat to be hidden from AG-UI too.");

        var asked = await RunAsync(http, Input("Close ticket 1234."));
        var call = asked.FirstOrDefault(item => Is(item, "TOOL_CALL_START"));
        if (Text(call, "toolCallName") != "close_ticket" || !Finished(asked, "interrupt"))
            throw new InvalidOperationException("Expected the call of close_ticket, then a run that waits.");

        var interrupt = asked[^1].GetProperty("outcome").GetProperty("interrupts")[0];
        var decision = interrupt.GetProperty("metadata");
        if (Text(decision, "kind") != "approval" || Text(decision, "tool") != "close_ticket"
            || Text(decision, "id") != Text(interrupt, "id"))
            throw new InvalidOperationException("Expected the decision as the interrupt, by its id.");

        var approval = Input("Close ticket 1234.");
        approval.Resume =
        [
            new AGUIResume
            {
                InterruptId = interrupt.GetProperty("id").GetString()!,
                Status = ResumeStatus.Resolved,
                Payload = JsonElement.Parse("""{"approved":true}""")
            }
        ];

        var resumed = await RunAsync(http, approval);
        if (!resumed.Any(item => Is(item, "TOOL_CALL_RESULT") && Text(item, "toolCallId") == Text(call, "toolCallId"))
            || !resumed.Any(item => Is(item, "TEXT_MESSAGE_CONTENT"))
            || !Finished(resumed, "success"))
            throw new InvalidOperationException("Expected the approved call's result, then the answer.");

        var twice = await PostAsync(http, approval);
        if (twice.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException("Expected an interrupt answered again to conflict.");

        var questions = await RunAsync(http, Input("Open a ticket for my dishwasher."));
        var asking = questions[^1].GetProperty("outcome").GetProperty("interrupts")[0];
        if (Text(asking.GetProperty("metadata"), "kind") != "question")
            throw new InvalidOperationException("Expected the questions as the interrupt.");

        var answers = Input("Open a ticket for my dishwasher.");
        answers.Resume =
        [
            new AGUIResume
            {
                InterruptId = asking.GetProperty("id").GetString()!,
                Status = ResumeStatus.Resolved,
                Payload = JsonElement.Parse("""{"answers":[{"selected":["It leaks"]},{"text":"Since Monday."}]}""")
            }
        ];

        if (!Finished(await RunAsync(http, answers), "success"))
            throw new InvalidOperationException("Expected the run to go on with the answers.");

        var failed = await RunAsync(http, Input(dropped));
        if (!Is(failed[^1], "RUN_ERROR") || failed.Any(item => item.GetRawText().Contains(reason)))
            throw new InvalidOperationException("Expected the dropped answer to end in RUN_ERROR, not the exception.");
    }

    // A message to a chat, whose id is the thread id too
    public static Task<HttpResponseMessage> PostAsync(HttpClient http, string text, int chat) =>
        PostAsync(http, Input(text, chat));

    private static RunAgentInput Input(string text, int chat = 48) =>
        new()
        {
            ThreadId = chat.ToString(CultureInfo.InvariantCulture),
            RunId = Guid.NewGuid().ToString("N"),
            Messages = [new AGUIUserMessage { Id = Guid.NewGuid().ToString("N"), Content = text }]
        };

    private static async Task<JsonElement[]> RunAsync(HttpClient http, RunAgentInput input)
    {
        var response = await PostAsync(http, input);
        response.EnsureSuccessStatusCode();

        // Each event is one data line of JSON
        var events = await response.Content.ReadAsStringAsync();
        return
        [
            .. events
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                .Select(item => JsonElement.Parse(item["data: ".Length..]))
        ];
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, RunAgentInput input)
    {
        var json = JsonSerializer.Serialize(input, ChatEndpoints.AgUiJson);
        var body = new StringContent(json, Encoding.UTF8, "application/json");

        return http.PostAsync($"/chats/{input.ThreadId}/ag-ui", body);
    }

    private static bool Finished(JsonElement[] events, string outcome) =>
        events is [.., var last] && Is(last, "RUN_FINISHED") && Text(last.GetProperty("outcome"), "type") == outcome;

    private static bool Is(JsonElement item, string type) => Text(item, "type") == type;

    private static string? Text(JsonElement item, string member) =>
        item.ValueKind is JsonValueKind.Object && item.TryGetProperty(member, out var value)
            ? value.GetString()
            : null;
}
