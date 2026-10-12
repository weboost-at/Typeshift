namespace Typeshift.Generators.Models;

// One services.AddTypeshift() call: the attribute that intercepts it and where it is
internal sealed record InterceptModel(string Attribute, LocationInfo? Location);
