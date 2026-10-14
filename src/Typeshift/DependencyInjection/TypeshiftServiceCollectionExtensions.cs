using Typeshift;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The entry point of the registration.
/// </summary>
public static class TypeshiftServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        // D1: the generator intercepts every call; this body runs only for one it did not see
        /// <summary>
        /// Registers every toolset and agent of this project and of the projects it references, an
        /// <see cref="AgentHost{TAgent}"/> per agent, and the core services. The generator intercepts the call; one it
        /// did not intercept throws, saying what to check.
        /// </summary>
        /// <returns>The chain that registers the chat client provider and the rest.</returns>
        /// <exception cref="InvalidOperationException">The call was not intercepted.</exception>
        public TypeshiftBuilder AddTypeshift() =>
            throw new InvalidOperationException(
                "AddTypeshift() was not intercepted by the Typeshift generator. Reference the Typeshift package in "
                + "the project that calls it, call it as services.AddTypeshift(), and keep the Typeshift.Generated "
                + "namespace in InterceptorsNamespaces.");
    }
}
