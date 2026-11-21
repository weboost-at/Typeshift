namespace Typeshift;

// D85: what a public member runs below it, Microsoft.Extensions.AI's tool loop and the app's own code, awaits without
// ConfigureAwait(false) and would come back to the caller's context. An async method calls Leave first, an iterator
// also each time it resumes: the builder puts the caller's context back when the method returns to its caller, so
// only the method's own steps go without it. A method that is not async never calls it, or its caller loses its context
internal static class SynchronizationContexts
{
    public static void Leave() => SynchronizationContext.SetSynchronizationContext(null);
}
