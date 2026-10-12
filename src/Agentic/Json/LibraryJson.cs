using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Agentic.Conversations;
using Microsoft.Extensions.AI;

namespace Agentic.Json;

// The contracts of the library's own stored data: they must not change with the developer's JSON options
internal static class LibraryJson
{
    public static JsonTypeInfo<StoredConversation> StoredConversation { get; } =
        (JsonTypeInfo<StoredConversation>)CreateOptions().GetTypeInfo(typeof(StoredConversation));

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                AgentJsonContext.Default,
                AIJsonUtilities.DefaultOptions.TypeInfoResolver)
        };

        options.MakeReadOnly();
        return options;
    }
}
