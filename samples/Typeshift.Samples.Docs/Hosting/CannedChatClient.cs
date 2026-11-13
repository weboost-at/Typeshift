using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// Stands in for a provider's client, so the guide's scenarios run without a network. It answers as a model would in
// each scenario: a plan in plan mode, a call of close_ticket when asked to close a ticket, of a sub-agent it is
// offered, a handoff to billing about a charge, of find_companies for the city it is given, JSON when the run asks for
// it, otherwise one fixed answer; once a tool answered, it answers in text, listing the companies find_companies found
internal sealed class CannedChatClient : IChatClient
{
    public const string Answer = "Your refund was approved and reaches your account within five days.";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(Respond([.. messages], options)) { ModelId = options?.ModelId });

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var message = Respond([.. messages], options);
        yield return new ChatResponseUpdate(message.Role, message.Contents) { ModelId = options?.ModelId };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static ChatMessage Respond(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        var offers = (options?.Tools ?? []).Select(tool => tool.Name).ToHashSet();
        var answered = messages.Count > 0 && messages[^1].Role == ChatRole.Tool;
        var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";

        AIContent content = (answered, options?.ResponseFormat) switch
        {
            (false, _) when offers.Contains("exit_plan_mode") =>
                Call("exit_plan_mode", ("plan", "1. Read the ticket. 2. Close it with the refund's date.")),
            (false, _) when offers.Contains("close_ticket") && input.StartsWith("Close ticket") =>
                Call("close_ticket", ("number", 1234), ("reason", "The refund was paid.")),
            (false, _) when offers.Contains("research_agent") =>
                Call("research_agent", ("input", "Vienna")),
            (false, _) when offers.Contains("handoff_to_billing_agent") && input.Contains("charged") =>
                Call("handoff_to_billing_agent"),
            (_, ChatResponseFormatJson) =>
                new TextContent("""{ "Invoice": "INV-7", "State": "Approved", "Expected": "2026-11-02" }"""),
            (false, _) when offers.Contains("find_companies") => Call("find_companies", ("city", input)),
            (true, _) when offers.Contains("find_companies") => new TextContent(Companies(messages[^1])),
            _ => new TextContent(Answer)
        };

        return new ChatMessage(ChatRole.Assistant, [content]);
    }

    // Getting started shows this answer, so it lists what the tool returned, as a model would
    private static string Companies(ChatMessage results)
    {
        var names = results.Contents
            .OfType<FunctionResultContent>()
            .Select(result => result.Result)
            .OfType<JsonElement>()
            .Where(found => found.ValueKind == JsonValueKind.Array)
            .SelectMany(found => found.EnumerateArray())
            .Select(company => company.GetProperty("name").GetString())
            .ToArray();

        return names switch
        {
            [] => "Found no companies.",
            [var only] => $"Found {only}.",
            [.. var others, var last] => $"Found {string.Join(", ", others)} and {last}."
        };
    }

    private static FunctionCallContent Call(string name, params (string Name, object Value)[] arguments) =>
        new(
            $"call_{Guid.NewGuid():N}",
            name,
            arguments.ToDictionary(argument => argument.Name, argument => (object?)argument.Value));
}
