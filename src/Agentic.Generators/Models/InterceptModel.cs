namespace Agentic.Generators.Models;

// One services.AddAgentic() call: the attribute that intercepts it and where it is
internal sealed record InterceptModel(string Attribute, LocationInfo? Location);
