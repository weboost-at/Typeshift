using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Typeshift.Samples.Companies;

namespace Typeshift.Samples.Chat;

// Stands in for a provider: it asks for find_companies with the user's city, or for count_companies when the user
// names a sector, then answers from the tool's result, in JSON when the run asks for it
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
            : new ChatMessage(ChatRole.Assistant, [Call(last.Text)]);

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
        var found = results.Select(result => result.Result).OfType<JsonElement>().First();

        // A model reads the sector by its name, which is a JSON string only where the enum travels as one
        if (found.ValueKind == JsonValueKind.Object)
            return $"{found.GetProperty("count")} companies work in {found.GetProperty("sector").GetString()}.";

        var names = found.EnumerateArray().Select(company => company.GetProperty("name").GetString()).ToList();
        var city = history.First(message => message.Role == ChatRole.User).Text;

        return options?.ResponseFormat is ChatResponseFormatJson
            ? $$"""{ "city": "{{city}}", "count": {{names.Count}} }"""
            : $"Found {string.Join(", ", names)}.";
    }

    // The sector goes out as its name, as a model sends an enum whose schema lists the names
    private static FunctionCallContent Call(string input) =>
        Enum.TryParse<Sector>(input, out _)
            ? new("call_count", "count_companies", new Dictionary<string, object?> { ["sector"] = input })
            : new("call_find", "find_companies", new Dictionary<string, object?> { ["city"] = input });
}
