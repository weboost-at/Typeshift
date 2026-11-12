namespace Typeshift.Generators.Emitting;

// D56: the provider interface under the name the docs use, closed over the build's provider id type; settings and
// conversation ids are typed in code, so the id-carrying types take theirs (D72)
internal static class AliasEmitter
{
    public static string Emit(string providerIdType) =>
        SourceWriter.Header
        + $"""

           global using IChatClientProvider = global::Typeshift.IChatClientProvider<{providerIdType}>;

           """;
}
