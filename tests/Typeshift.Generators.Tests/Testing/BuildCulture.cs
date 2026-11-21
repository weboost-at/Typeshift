using System.Globalization;

namespace Typeshift.Generators.Tests.Testing;

// The culture of the machine that builds, for as long as a test holds it
public sealed class BuildCulture : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public BuildCulture(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
