using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Agentic.Settings;

namespace Agentic.Json;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StoredSettings))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(float?))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(IList<string>))]
[JsonSerializable(typeof(TimeSpan?))]
[JsonSerializable(typeof(DateTimeOffset?))]
[JsonSerializable(typeof(Guid?))]
internal sealed partial class AgentJsonContext : JsonSerializerContext;
