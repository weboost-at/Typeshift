using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Agentic.Generators.Models;

// An equatable stand-in for Location, turned back into one when the diagnostic is reported
internal sealed record LocationInfo(string Path, TextSpan Span, LinePositionSpan Lines)
{
    public static LocationInfo? From(Location? location) =>
        location?.SourceTree is { } tree ? new(tree.FilePath, location.SourceSpan, location.GetLineSpan().Span) : null;

    public Location ToLocation() => Location.Create(Path, Span, Lines);
}
