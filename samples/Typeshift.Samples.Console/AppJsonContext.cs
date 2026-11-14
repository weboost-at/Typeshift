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
