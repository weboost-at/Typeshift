// The Microsoft.Extensions.DependencyInjection namespace makes AddAgentic() available without a using
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the toolsets and agents of a host and of its references.
/// </summary>
public static class AgenticServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the library and every toolset, agent and agent runtime the calling project can see.
        /// </summary>
        public IServiceCollection AddAgentic() =>
            throw new InvalidOperationException(
                "AddAgentic() was not intercepted, so nothing was registered. The project that calls it must "
                + "reference the Agentic package itself (analyzers do not flow to referencing projects), build with "
                + "the .NET 10 SDK and keep Agentic.Generated in its InterceptorsNamespaces. Call it directly, as "
                + "services.AddAgentic(); a call through a delegate cannot be intercepted.");
    }
}
