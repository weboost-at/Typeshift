using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Typeshift.Generated;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(JsonObject))]
internal sealed partial class ToolJsonContext : JsonSerializerContext;
