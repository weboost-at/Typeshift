using System.Text.Json;
using System.Text.Json.Serialization;
using Typeshift.Samples.Companies;

namespace Typeshift.Samples;

// begin-snippet: json-context
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(CompanyCount))]
[JsonSerializable(typeof(Sector))]
[JsonSerializable(typeof(SectorCount))]
internal partial class AppJsonContext : JsonSerializerContext;
// end-snippet

// What an endpoint returns and takes: a decision as the UI shows it, and the answer the UI posts back
// begin-snippet: decision-json-context
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Decision))]
[JsonSerializable(typeof(DecisionAnswer))]
internal partial class EndpointJsonContext : JsonSerializerContext;
// end-snippet
