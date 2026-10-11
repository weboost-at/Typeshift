using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Agentic.Agents;

internal sealed class TypedChatOptionProperty<TValue>(
    string name,
    Func<ChatOptions, TValue> get,
    Action<ChatOptions, TValue> set) : ChatOptionProperty(name)
{
    internal override void Apply(ChatOptions options, JsonElement stored, JsonSerializerOptions serializerOptions) =>
        set(options, stored.Deserialize((JsonTypeInfo<TValue>)serializerOptions.GetTypeInfo(typeof(TValue)))!);

    // A null override inherits, it never clears a value
    internal override void Override(ChatOptions options, ChatOptions overrides)
    {
        if (get(overrides) is { } value) set(options, value);
    }
}
