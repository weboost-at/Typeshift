using System.Text.Json.Serialization;

namespace Typeshift.Tests.Toolsets.Companies;

// Internal, so only the generated module can register it
[JsonSerializable(typeof(FindCompaniesSettings))]
internal partial class CompanyJsonContext : JsonSerializerContext;
