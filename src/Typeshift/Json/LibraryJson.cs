using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typeshift.Conversations;
using Microsoft.Extensions.AI;

namespace Typeshift.Json;

// The contracts of the library's own stored data: they must not change with the developer's JSON options
internal static class LibraryJson
{
    public static JsonTypeInfo<StoredConversation> StoredConversation { get; } =
        (JsonTypeInfo<StoredConversation>)CreateOptions([]).GetTypeInfo(typeof(StoredConversation));

    // The given resolvers come first, then the library's own context, then the defaults of Microsoft.Extensions.AI
    public static JsonSerializerOptions CreateOptions(IEnumerable<IJsonTypeInfoResolver> resolvers)
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                [.. resolvers, AgentJsonContext.Default, AIJsonUtilities.DefaultOptions.TypeInfoResolver])
        };

        options.MakeReadOnly();
        return options;
    }
}
