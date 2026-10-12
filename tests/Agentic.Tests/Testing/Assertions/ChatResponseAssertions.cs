using System.Text.Json;
using Agentic.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Testing.Assertions;

public sealed class ChatResponseAssertions(ChatResponse response)
{
    public T ToolResult<T>(string callId, AgentJson json)
    {
        var result = ToolResult(callId);
        result.Exception.Should().BeNull();
        result.Result.Should().BeOfType<JsonElement>();
        return ((JsonElement)result.Result!).Deserialize(json.GetTypeInfo<T>())!;
    }

    public FunctionResultContent ToolResult(string callId)
    {
        var results = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Where(result => result.CallId == callId)
            .ToList();

        results.Should().ContainSingle();
        return results[0];
    }
}

public static class ChatResponseAssertionExtensions
{
    extension(ChatResponse response)
    {
        public ChatResponseAssertions Assert => new(response);
    }
}
