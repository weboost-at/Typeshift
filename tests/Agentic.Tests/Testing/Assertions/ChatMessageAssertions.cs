using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Testing.Assertions;

public sealed class ChatMessageAssertions(ChatMessage message)
{
    public ErrorContent Error()
    {
        var errors = message.Contents.OfType<ErrorContent>().ToList();

        message.Role.Should().Be(ChatRole.Assistant);
        errors.Should().ContainSingle();
        return errors[0];
    }
}

public static class ChatMessageAssertionExtensions
{
    extension(ChatMessage message)
    {
        public ChatMessageAssertions Assert => new(message);
    }
}
