using System.Globalization;

namespace Typeshift.Tests.Persistence.Support;

// Makes the current culture one that writes numbers unlike the invariant culture, until it is disposed
public sealed class NumberCulture : IDisposable
{
    private readonly CultureInfo _before = CultureInfo.CurrentCulture;

    private NumberCulture(Action<NumberFormatInfo> change)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        change(culture.NumberFormat);

        CultureInfo.CurrentCulture = culture;
    }

    // Negative numbers with the minus sign U+2212, as Swedish and Norwegian write them
    public static NumberCulture WithMinusSign() => new(numbers => numbers.NegativeSign = "−");

    // A comma before the decimals, as German and Danish write them
    public static NumberCulture WithDecimalComma() => new(numbers => numbers.NumberDecimalSeparator = ",");

    public void Dispose() => CultureInfo.CurrentCulture = _before;
}
