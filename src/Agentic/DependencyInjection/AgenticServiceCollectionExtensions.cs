using System.Diagnostics.CodeAnalysis;
using Agentic.Conversations;
using Agentic.Persistence;

// The Microsoft.Extensions.DependencyInjection namespace makes AddAgentic() available without a using
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the toolsets and agents of a host and of its references, and the store they persist through.
/// </summary>
public static class AgenticServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the library and every toolset, agent and agent runtime the calling project can see. The
        /// generator replaces this call at compile time; the method itself only runs when that did not happen.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The call was not intercepted; the message says why.</exception>
        public IServiceCollection AddAgentic() =>
            throw new InvalidOperationException(
                "AddAgentic() was not intercepted, so nothing was registered. The project that calls it must "
                + "reference the Agentic package itself (analyzers do not flow to referencing projects), build with "
                + "the .NET 10 SDK and keep Agentic.Generated in its InterceptorsNamespaces. Call it directly, as "
                + "services.AddAgentic(); a call through a delegate cannot be intercepted.");

        /// <summary>
        /// Registers the store the library persists through: agent settings, tool defaults and conversations.
        /// Without a store, the code alone defines every agent and the APIs that save or load throw.
        /// </summary>
        /// <typeparam name="TStore">Your store, registered as a singleton.</typeparam>
        /// <typeparam name="TId">
        /// The id type of your store. Conversation ids are parsed into it, so it must be parsable, as
        /// <see cref="Guid"/>, <see cref="string"/> and <see cref="int"/> are.
        /// </typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddAgenticStore<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore,
            TId>()
            where TStore : class, IAgenticStore<TId>
            where TId : notnull, IParsable<TId>
        {
            services.AddSingleton<IAgenticStore<TId>, TStore>();
            services.AddSingleton<ConversationStore>(provider =>
                new ConversationStore<TId>(provider.GetRequiredService<IAgenticStore<TId>>()));

            return services;
        }
    }
}
