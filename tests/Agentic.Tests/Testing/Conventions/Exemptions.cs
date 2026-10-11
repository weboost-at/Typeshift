namespace Agentic.Tests.Testing.Conventions;

// Today's violators, a ratchet: an entry goes when it is fixed and nothing new is ever added
public static class Exemptions
{
    // Class.Method
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>();

    // file: Method
    public static IReadOnlySet<string> LongDeclarations { get; } = new HashSet<string>();

    // Namespace.Class
    public static IReadOnlySet<string> Misplaced { get; } = new HashSet<string>();

    // file: Method
    public static IReadOnlySet<string> Comments { get; } = new HashSet<string>();

    // file: Type
    public static IReadOnlySet<string> SupportOutsideSupport { get; } = new HashSet<string>();

    // file: banned call, for example "Agentic.Tests/Conversations/Support/ChatTestData.cs: wall clock"
    public static IReadOnlySet<string> BannedCalls { get; } = new HashSet<string>();

    public static IEnumerable<string> All =>
        Names.Concat(LongDeclarations)
            .Concat(Misplaced)
            .Concat(Comments)
            .Concat(SupportOutsideSupport)
            .Concat(BannedCalls);
}
