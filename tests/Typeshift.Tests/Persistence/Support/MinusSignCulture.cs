using System.Globalization;

namespace Typeshift.Tests.Persistence.Support;

// Makes the current culture one that writes negative numbers with the minus sign U+2212, as Swedish and Norwegian
// do, until it is disposed
public sealed class MinusSignCulture : IDisposable
{
    private readonly CultureInfo _before = CultureInfo.CurrentCulture;

    public MinusSignCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "−";

        CultureInfo.CurrentCulture = culture;
    }

    public void Dispose() => CultureInfo.CurrentCulture = _before;
}
