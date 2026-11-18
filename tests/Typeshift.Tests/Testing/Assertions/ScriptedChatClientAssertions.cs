using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Testing.Assertions;

public sealed class ScriptedChatClientAssertions(ScriptedChatClient client)
{
    // Each scripted reply is a model call the test expects: one left over, or a call past the script, fails it
    public void Drained()
    {
        var unused = client.Unused;
        var unscripted = client.Unscripted;

        Execute.Assertion
            .ForCondition(unused.Count == 0)
            .FailWith("The run never asked for {0} of the test's scripted replies: {1}.", unused.Count, unused);

        Execute.Assertion
            .ForCondition(unscripted.Count == 0)
            .FailWith("The run made {0} model calls past the script, after: {1}.", unscripted.Count, unscripted);
    }

    // The model call at that position, from the first or from the end, failing with how many calls the run made
    public ScriptedRequest Request(Index index)
    {
        var count = client.Requests.Count;
        var offset = index.GetOffset(count);
        var expected = index.IsFromEnd ? index.Value : offset + 1;

        Execute.Assertion
            .ForCondition(offset >= 0 && offset < count)
            .FailWith($"The run made {Calls(count)}, expected at least {Calls(expected)}.");

        return new(client.Requests[offset], client.Options[offset]);
    }

    private static string Calls(int count) => count == 1 ? "1 model call" : $"{count} model calls";
}

// What one model call carried: its messages, its options, and readers for what a test asks of them
public sealed record ScriptedRequest(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)
{
    public List<string> Roles => [.. Messages.Select(message => message.Role.Value)];

    public List<string> Texts => [.. Messages.Select(message => message.Text)];

    // The id of each tool call the messages hold
    public List<string> Calls => [.. Contents<FunctionCallContent>().Select(call => call.CallId)];

    // Each tool result the model read, as its text
    public List<string> Results => [.. ResultContents.Select(result => result.Result?.ToString() ?? "")];

    // The one tool result the model read, as its text
    public string Result => Single(Results);

    // The one tool result the model read, as the value the tool loop handed on
    public object? ResultValue => Single([.. ResultContents]).Result;

    // The names of the tools the call offered the model
    public List<string> Tools => [.. Options?.Tools?.Select(tool => tool.Name) ?? []];

    private IEnumerable<FunctionResultContent> ResultContents => Contents<FunctionResultContent>();

    private IEnumerable<T> Contents<T>() => Messages.SelectMany(message => message.Contents).OfType<T>();

    private static T Single<T>(IReadOnlyList<T> items)
    {
        items.Should().ContainSingle();
        return items[0];
    }
}

public static class ScriptedChatClientAssertionExtensions
{
    extension(ScriptedChatClient client)
    {
        public ScriptedChatClientAssertions Assert => new(client);
    }
}
