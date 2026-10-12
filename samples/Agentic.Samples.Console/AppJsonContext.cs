using System.Text.Json.Serialization;
using Agentic.Samples.Companies;

namespace Agentic.Samples;

// begin-snippet: json-context
// Tool parameters and results serialize through source-generated metadata, which keeps the sample AOT-safe
[JsonSerializable(typeof(IReadOnlyList<Company>))]
internal partial class AppJsonContext : JsonSerializerContext;
// end-snippet
