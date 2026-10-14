using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// Stands in for a provider: it plans with write_todos and asks for find_companies with the user's city, then answers
// from the tool's result, in JSON when the run asks for it
internal sealed class ScriptedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var history = messages.ToList();
        var last = history[^1];

        var message = last.Contents.OfType<FunctionResultContent>().ToList() is { Count: > 0 } results
            ? new ChatMessage(ChatRole.Assistant, Answer(history, results, options))
            : new ChatMessage(ChatRole.Assistant, [Plan(), FindCompanies(last.Text)]);

        return Task.FromResult(new ChatResponse(message)
        {
            ModelId = options?.ModelId,
            Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30, TotalTokenCount = 150 }
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);

        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    // The tool's result reaches the model as JSON, as a provider would send it
    private static string Answer(List<ChatMessage> history, List<FunctionResultContent> results, ChatOptions? options)
    {
        var found = results
            .Select(result => result.Result)
            .OfType<JsonElement>()
            .First(result => result.ValueKind == JsonValueKind.Array);

        var names = found.EnumerateArray().Select(company => company.GetProperty("name").GetString()).ToList();
        var city = history.First(message => message.Role == ChatRole.User).Text;

        return options?.ResponseFormat is ChatResponseFormatJson
            ? $$"""{ "city": "{{city}}", "count": {{names.Count}} }"""
            : $"Found {string.Join(", ", names)}.";
    }

    private static FunctionCallContent Plan() =>
        new("call_plan", "write_todos", new Dictionary<string, object?>
        {
            ["todos"] = JsonElement.Parse("""[{ "title": "Find the companies", "status": "InProgress" }]""")
        });

    private static FunctionCallContent FindCompanies(string city) =>
        new("call_find", "find_companies", new Dictionary<string, object?> { ["city"] = city });
}
