using System.Text.Json.Serialization;
using Typeshift.Samples.Companies;

namespace Typeshift.Samples;

// begin-snippet: json-context
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(CompanyCount))]
internal partial class AppJsonContext : JsonSerializerContext;
// end-snippet
