using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Typeshift.Samples.Companies;

namespace Typeshift.Samples.Chat;

// Stands in for a provider: it asks for find_companies with the user's city, for count_companies when the user
// names a sector, or for get_logos with the city the user asks the logos of, then answers from the tool's result, in
// JSON when the run asks for it. Offered ask_user, it asks
// which company to write to, calls send_mail with the user's answer, then answers with what the tool returned
internal sealed class ScriptedChatClient : IChatClient
{
    private static readonly JsonElement Questions = JsonElement.Parse(
        """
        [
          {
            "question": "Which company should the mail go to?",
            "header": "Company",
            "options": [
              { "label": "Danube Robotics", "description": "Builds robot arms." },
              { "label": "Prater Motion", "description": "Builds drives." }
            ]
          }
        ]
        """);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var history = messages.ToList();
        var last = history[^1];
        var results = last.Contents.OfType<FunctionResultContent>().ToList();
        var asks = options?.Tools?.Any(tool => tool.Name == BuiltInTools.AskUser) is true;

        AIContent content = (asks, results) switch
        {
            (true, []) => new FunctionCallContent("call_ask", "ask_user", Arguments("questions", Questions)),
            (true, [{ CallId: "call_ask" } answers]) => SendMail(answers),
            (true, [var sent]) => new TextContent($"{sent.Result}"),
            (false, []) => Call(last.Text),
            (false, [{ Result: IEnumerable<AIContent> logos }]) => new TextContent(Logos(logos)),
            _ => new TextContent(Answer(history, results, options))
        };

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [content]))
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

    // The tool's result reaches the model as the text of its JSON, as a provider sends it
    private static string Answer(List<ChatMessage> history, List<FunctionResultContent> results, ChatOptions? options)
    {
        var found = JsonElement.Parse(results.Select(result => result.Result).OfType<string>().First());

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
        input switch
        {
            _ when Enum.TryParse<Sector>(input, out _) =>
                new("call_count", "count_companies", Arguments("sector", input)),
            _ when input.StartsWith(LogosOf, StringComparison.Ordinal) =>
                new("call_logos", CompanyToolset.ToolNames.GetLogos, Arguments("city", input[LogosOf.Length..])),
            _ => new("call_find", "find_companies", Arguments("city", input))
        };

    private const string LogosOf = "Logos of ";

    // The logos reach the model as images, never as the text of their JSON
    private static string Logos(IEnumerable<AIContent> logos) =>
        $"{logos.OfType<DataContent>().Count(logo => logo.MediaType == "image/png")} logos.";

    // The answers reach the model as the JSON ask_user returns: the picked label under the question's header
    private static FunctionCallContent SendMail(FunctionResultContent answers)
    {
        using var result = JsonDocument.Parse($"{answers.Result}");
        var company = result.RootElement.GetProperty("answers")[0].GetProperty("selected")[0].GetString();

        return new FunctionCallContent("call_mail", "send_mail", Arguments("company", company));
    }

    private static Dictionary<string, object?> Arguments(string name, object? value) => new() { [name] = value };
}
