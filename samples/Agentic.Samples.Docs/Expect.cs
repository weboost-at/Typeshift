namespace Agentic.Samples.Docs;

internal static class Expect
{
    public static void That(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"A documented scenario broke: {what}.");
    }
}
