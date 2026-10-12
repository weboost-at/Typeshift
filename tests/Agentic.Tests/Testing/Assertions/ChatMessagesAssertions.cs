using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Testing.Assertions;

public sealed class ChatMessagesAssertions(IReadOnlyList<ChatMessage> messages)
{
    // Each message by its role and the kind of its content
    public IReadOnlyList<ChatMessage> Shaped(params (ChatRole Role, Type Content)[] shape)
    {
        messages
            .Select(message => (message.Role, message.Contents.Single().GetType()))
            .Should()
            .Equal(shape);

        return messages;
    }

    public IReadOnlyList<ChatMessage> Texts(params string[] texts)
    {
        messages.Select(message => message.Text).Should().Equal(texts);
        return messages;
    }
}

public static class ChatMessagesAssertionExtensions
{
    extension(IReadOnlyList<ChatMessage> messages)
    {
        public ChatMessagesAssertions Assert => new(messages);
    }
}
