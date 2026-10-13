using System.Text.Json.Serialization;

namespace Typeshift.Tests.Toolsets.Support;

[JsonSerializable(typeof(ProbeResult))]
internal partial class ProbeJsonContext : JsonSerializerContext;
