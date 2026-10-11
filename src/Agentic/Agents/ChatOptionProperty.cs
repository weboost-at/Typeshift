using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// One member of <see cref="ChatOptions"/> that may change at runtime, read and set without reflection.
/// </summary>
public abstract class ChatOptionProperty
{
    private protected ChatOptionProperty(string name) => Name = name;

    /// <summary>
    /// The property name, also the key of its stored value.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A property read by <paramref name="get"/> and written by <paramref name="set"/>.
    /// </summary>
    public static ChatOptionProperty Create<TValue>(
        string name,
        Func<ChatOptions, TValue> get,
        Action<ChatOptions, TValue> set) =>
        new TypedChatOptionProperty<TValue>(name, get, set);

    internal abstract void Apply(ChatOptions options, JsonElement stored, JsonSerializerOptions serializerOptions);

    internal abstract void Override(ChatOptions options, ChatOptions overrides);
}
