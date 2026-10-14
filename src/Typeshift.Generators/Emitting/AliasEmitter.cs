namespace Typeshift.Generators.Emitting;

// D11: the library's id-carrying types under the names the docs use, closed over the build's id type
internal static class AliasEmitter
{
    public static string Emit(string idType) =>
        SourceWriter.Header
        + $"""

           global using ITypeshiftStore = global::Typeshift.ITypeshiftStore<{idType}>;
           global using InMemoryTypeshiftStore = global::Typeshift.InMemoryTypeshiftStore<{idType}>;

           """;
}
