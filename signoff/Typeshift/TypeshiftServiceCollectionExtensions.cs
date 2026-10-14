using Typeshift;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The entry point of the registration.
/// </summary>
public static class TypeshiftServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers every toolset and agent of this project and of the projects it references, an
        /// <see cref="AgentHost{TAgent}"/> per agent, and the core services. The generator intercepts the call; one it
        /// did not intercept throws, saying what to check.
        /// </summary>
        /// <returns>The chain that registers the store, the chat client provider and the rest.</returns>
        public TypeshiftBuilder AddTypeshift() => throw null!;
    }
}
