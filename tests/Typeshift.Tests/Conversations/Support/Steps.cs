using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations.Support;

// The steps a run stores, built by hand, for a test that hands a conversation to the code that reads it
public static class Steps
{
    private const string Agent = "probe_agent";

    public static ConversationEntry Entry(params ChatMessage[] messages) =>
        new(Agent, messages, DateTimeOffset.UnixEpoch);

    public static ChatMessage Input(string text) => new(ChatRole.User, text);

    public static ChatMessage Calling(params string[] callIds) =>
        new(ChatRole.Assistant, [.. callIds.Select(callId => new FunctionCallContent(callId, "probe"))]);

    public static ChatMessage Results(params string[] callIds) =>
        new(ChatRole.Tool, [.. callIds.Select(callId => new FunctionResultContent(callId, $"result of {callId}"))]);

    public static ChatMessage Answer(string text) => new(ChatRole.Assistant, text);
}
