using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// Stands in for a provider's client, so the guide's scenarios run without a network. It answers as a model would in
// each scenario: a plan in plan mode, a call of the tool the input asks for when it is offered, such as "Close ticket",
// "Run" a command or "Show the screenshot" of a ticket, two order lookups in one response, questions to the user
// before it opens a ticket, a call of a sub-agent it is offered, a handoff to billing about a charge, of
// find_companies for the city it is given, JSON when the run asks for it, otherwise one fixed answer; once a tool
// answered, it answers in text, listing the companies find_companies found. It fails the first call of one input, as
// a provider that is down does. Like a provider's client, it stops at a cancelled token
internal sealed class CannedChatClient : IChatClient
{
    public const string Answer = "Your refund was approved and reaches your account within five days.";

    // An input the client fails once, with the HTTP error of a provider that is down, and answers after
    public const string Unavailable = "Is the provider up?";

    private int _failed;

    // What a model asks before it opens a ticket: one choice, and one question the user answers in their own words
    private static readonly JsonElement Questions = JsonElement.Parse(
        """
        [
          {
            "question": "What is wrong with the appliance?",
            "header": "Problem",
            "options": [
              { "label": "It leaks", "description": "Water under or around it." },
              { "label": "It does not start", "description": "No light and no sound." },
              { "label": "It is loud", "description": "A noise it did not make before." }
            ]
          },
          {
            "question": "Since when?",
            "header": "Since",
            "options": [
              { "label": "Today", "description": "It worked yesterday." },
              { "label": "This week", "description": "It worked last week." }
            ]
          }
        ]
        """);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<ChatResponse>(cancellationToken)
            : Task.FromResult(new ChatResponse(Respond([.. messages], options)) { ModelId = options?.ModelId });

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var message = Respond([.. messages], options);
        yield return new ChatResponseUpdate(message.Role, message.Contents) { ModelId = options?.ModelId };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private ChatMessage Respond(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        if (messages.LastOrDefault()?.Text == Unavailable && Interlocked.Exchange(ref _failed, 1) == 0)
            throw new HttpRequestException("Service unavailable.", null, System.Net.HttpStatusCode.ServiceUnavailable);

        var offers = (options?.Tools ?? []).Select(tool => tool.Name).ToHashSet();
        var answered = messages.Count > 0 && messages[^1].Role == ChatRole.Tool;
        var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";

        // Two calls in one response, which an agent with concurrent tool calls runs at the same time
        if (!answered && offers.Contains("get_order") && input.StartsWith("Where are orders "))
        {
            return new ChatMessage(
                ChatRole.Assistant,
                [Call("get_order", ("number", 1)), Call("get_order", ("number", 2))]);
        }

        AIContent content = (answered, options?.ResponseFormat) switch
        {
            (false, _) when offers.Contains("exit_plan_mode") =>
                Call("exit_plan_mode", ("plan", "1. Read the ticket. 2. Close it with the refund's date.")),
            (false, _) when offers.Contains("close_ticket") && input.StartsWith("Close ticket") =>
                Call("close_ticket", ("number", 1234), ("reason", "The refund was paid.")),
            (false, _) when offers.Contains("ask_user") && input.StartsWith("Open a ticket") =>
                Call("ask_user", ("questions", Questions)),
            (false, _) when offers.Contains("run_command") && input.StartsWith("Run ") =>
                Call("run_command", ("command", input["Run ".Length..])),
            (false, _) when offers.Contains("search_tickets") && input.StartsWith("Search tickets for ") =>
                Call("search_tickets", ("query", input["Search tickets for ".Length..])),
            (false, _) when offers.Contains("search_articles") && input.StartsWith("Search the help center for ") =>
                Call("search_articles", ("query", input["Search the help center for ".Length..])),
            (false, _) when offers.Contains("find_ticket") && input.StartsWith("Find ticket ") =>
                Call("find_ticket", ("key", Key(input["Find ticket ".Length..]))),
            (false, _) when offers.Contains("escalate_ticket") && input.StartsWith("Escalate ticket ") =>
                Call("escalate_ticket", ("number", int.Parse(input["Escalate ticket ".Length..]))),
            (false, _) when offers.Contains("get_screenshot") && input.StartsWith("Show the screenshot of ticket ") =>
                Call("get_screenshot", ("number", int.Parse(input["Show the screenshot of ticket ".Length..]))),
            (false, _) when offers.Contains("explore_agent") && input.StartsWith("Ask the explorer ") =>
                Call("explore_agent", ("input", input["Ask the explorer ".Length..])),
            (false, _) when offers.Contains("research_agent") =>
                Call("research_agent", ("input", "Vienna")),
            (false, _) when offers.Contains("handoff_to_billing_agent") && input.Contains("charged") =>
                Call("handoff_to_billing_agent"),
            (_, ChatResponseFormatJson { SchemaName: "CityCompanies" }) =>
                new TextContent("""{ "City": "Vienna", "Companies": ["Robotik Wien", "Danube Drives"] }"""),
            (_, ChatResponseFormatJson) =>
                new TextContent("""{ "Invoice": "INV-7", "State": "Approved", "Expected": "2026-11-02" }"""),
            (false, _) when offers.Contains("find_companies") => Call("find_companies", ("city", input)),
            (true, _) when offers.Contains("find_companies") => new TextContent(Companies(messages[^1])),
            _ => new TextContent(Answer)
        };

        return new ChatMessage(ChatRole.Assistant, [content]);
    }

    // Getting started shows this answer, so it lists what the tool returned, as a model would. A provider's client
    // receives a JSON result as its text
    private static string Companies(ChatMessage results)
    {
        var names = results.Contents
            .OfType<FunctionResultContent>()
            .Select(result => result.Result)
            .OfType<string>()
            .Where(found => found.StartsWith('['))
            .SelectMany(found => JsonElement.Parse(found).EnumerateArray())
            .Select(company => company.GetProperty("name").GetString())
            .ToArray();

        return names switch
        {
            [] => "Found no companies.",
            [var only] => $"Found {only}.",
            [.. var others, var last] => $"Found {string.Join(", ", others)} and {last}."
        };
    }

    // A ticket's number, or the words of its title
    private static object Key(string text) => int.TryParse(text, out var number) ? number : text;

    private static FunctionCallContent Call(string name, params (string Name, object Value)[] arguments) =>
        new(
            $"call_{Guid.NewGuid():N}",
            name,
            arguments.ToDictionary(argument => argument.Name, argument => (object?)argument.Value));
}
