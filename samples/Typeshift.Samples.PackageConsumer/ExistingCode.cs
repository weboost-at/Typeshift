namespace Typeshift.Samples.PackageConsumer;

// Comments as a project wrote them before it added the package. The compiler checks them only with XML docs on, so
// the two mistakes below show as warnings and must not fail a build that treats warnings as errors (D68)
internal static class ExistingCode
{
    /// <summary>Adds two numbers.</summary>
    /// <param name="a">The first.</param>
    /// <returns>The sum.</returns>
    internal static int Add(int a, int b) => a + b;

    /// <summary>See <see cref="Missing"/>.</summary>
    internal static int Zero => 0;
}
