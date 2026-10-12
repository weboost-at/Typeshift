using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Chat;

// Stands in for a provider: it asks for find_companies with the user's city, then answers with the tool's result
internal sealed class ScriptedChatClient(string model) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var last = messages.Last();

        var message = last.Contents.OfType<FunctionResultContent>().FirstOrDefault() is { } result
            ? new ChatMessage(ChatRole.Assistant, $"Found {Names(result)}.")
            : new ChatMessage(ChatRole.Assistant, [FindCompanies(last.Text)]);

        return Task.FromResult(new ChatResponse(message)
        {
            ModelId = model,
            FinishReason = message.Contents is [FunctionCallContent]
                ? ChatFinishReason.ToolCalls
                : ChatFinishReason.Stop,
            Usage = new UsageDetails
            {
                InputTokenCount = 120,
                OutputTokenCount = 30,
                TotalTokenCount = 150
            }
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await GetResponseAsync(messages, options, ct);

        foreach (var update in response.ToChatResponseUpdates()) yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    // The tool's result reaches the model as JSON, as a provider would send it
    private static string Names(FunctionResultContent result) =>
        string.Join(
            ", ",
            ((JsonElement)result.Result!).EnumerateArray().Select(company => company.GetProperty("name")));

    private static FunctionCallContent FindCompanies(string city) =>
        new($"call_{Guid.NewGuid():N}", "find_companies", new Dictionary<string, object?> { ["city"] = city });
}
