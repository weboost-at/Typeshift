namespace Typeshift.Generators.Emitting;

// D11, D56: the library's id-carrying types under the names the docs use, closed over the build's id types
internal static class AliasEmitter
{
    public static string Emit(string idType, string providerIdType) =>
        SourceWriter.Header
        + $"""

           global using ITypeshiftStore = global::Typeshift.ITypeshiftStore<{idType}>;
           global using InMemoryTypeshiftStore = global::Typeshift.InMemoryTypeshiftStore<{idType}>;
           global using ConversationTracker = global::Typeshift.ConversationTracker<{idType}>;
           global using LiveRun = global::Typeshift.LiveRun<{idType}>;
           global using IChatClientProvider = global::Typeshift.IChatClientProvider<{providerIdType}>;

           """;
}
