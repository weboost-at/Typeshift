using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Agentic.Conversations;

namespace Agentic.Json;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StoredConversation))]
[JsonSerializable(typeof(JsonObject))]
internal sealed partial class AgentJsonContext : JsonSerializerContext;
