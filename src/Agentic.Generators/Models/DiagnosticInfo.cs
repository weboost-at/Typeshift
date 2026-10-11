using System.Linq;
using Agentic.Generators.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Models;

// A diagnostic without Roslyn objects; the descriptor is looked up by id when it is reported
internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableArray<string> Arguments)
{
    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        LocationInfo? location,
        params string[] arguments) =>
        new(descriptor.Id, location, arguments.ToEquatableArray());

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(
            Descriptors.ById(Id),
            Location?.ToLocation(),
            Arguments.Cast<object>().ToArray());
}
