using System.Text.Json.Serialization;
using Typeshift.Samples.Companies;

namespace Typeshift.Samples;

// begin-snippet: json-context
// Tool parameters and results serialize through source-generated metadata, which keeps the sample AOT-safe
[JsonSerializable(typeof(IReadOnlyList<Company>))]
internal partial class AppJsonContext : JsonSerializerContext;
// end-snippet
