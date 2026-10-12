using Bogus;

namespace Agentic.Tests.Testing.Data;

public static class TestFaker
{
    // The published starting value and multiplier of 32-bit FNV-1a, a small and widely used string hash
    private const uint FnvOffsetBasis = 2166136261;

    private const uint FnvPrime = 16777619;

    // Seeded from the running test's identity, so a test draws the same values on every run
    public static Faker ForCurrentTest() =>
        new()
        {
            Random = new Randomizer(Seed(TestContext.Current.Test?.UniqueID ?? ""))
        };

    // string.GetHashCode differs between processes, FNV-1a gives a test the same seed on every run and machine
    private static int Seed(string identity)
    {
        var hash = FnvOffsetBasis;

        foreach (var character in identity)
        {
            hash = (hash ^ character) * FnvPrime;
        }

        // Randomizer takes an int, a seed past int.MaxValue simply turns negative
        return unchecked((int)hash);
    }
}
