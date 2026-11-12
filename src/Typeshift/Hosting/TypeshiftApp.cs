using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

/// <summary>
/// Typeshift for an app without a container of its own, such as a console tool or a script: every agent of this
/// project and of the projects it references, on one chat client. Create it once, keep it while the app runs, and
/// dispose it at the end. The app has one scope for its lifetime, so its scoped services, such as the chat client
/// provider, approval rules, middleware, the MCP tool provider and the services tools resolve, are created once and
/// shared by every run, concurrent ones included. An app with a container, or one that needs a scope per request,
/// calls <c>services.AddTypeshift()</c> instead.
/// </summary>
public sealed class TypeshiftApp : IAsyncDisposable, IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    internal TypeshiftApp(ServiceProvider provider)
    {
        _provider = provider;
        _scope = provider.CreateAsyncScope();
    }

    /// <summary>
    /// The services the app's agents run with, for what <see cref="Agent{TAgent}"/> does not reach, such as the
    /// <c>ConversationTracker</c>.
    /// </summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    // D1, D75: the generator intercepts every call; this body runs only for one it did not see
    /// <summary>
    /// Creates the app, with every toolset and agent of this project and of the projects it references, and with
    /// <paramref name="client"/> serving every agent's runs. The generator intercepts the call; one it did not
    /// intercept throws, saying what to check.
    /// </summary>
    /// <param name="client">The chat client of every run; the app never disposes it.</param>
    /// <param name="configure">
    /// Registers what else the app needs, as the chain of <c>AddTypeshift()</c> does, such as
    /// <c>typeshift => typeshift.WithStore&lt;MyStore&gt;()</c>; null for nothing.
    /// </param>
    /// <returns>The app.</returns>
    /// <exception cref="InvalidOperationException">The call was not intercepted.</exception>
    public static TypeshiftApp Create(IChatClient client, Action<TypeshiftBuilder>? configure = null) =>
        throw new InvalidOperationException(
            "TypeshiftApp.Create() was not intercepted by the Typeshift generator. Reference the Typeshift package in "
            + "the project that calls it, call it as TypeshiftApp.Create(client), not through using static, a method "
            + "group or a dynamic argument, and keep the Typeshift.Generated namespace in InterceptorsNamespaces.");

    /// <summary>
    /// The host of an agent, ready to run.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <returns>The host, with the agent's typed members.</returns>
    public AgentHost<TAgent> Agent<TAgent>() where TAgent : Agent =>
        _scope.ServiceProvider.GetRequiredService<AgentHost<TAgent>>();

    /// <summary>
    /// Disposes the app's services, which ends what they hold, such as stores your app did not create.
    /// </summary>
    /// <returns>The disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _scope.DisposeAsync();
        }
        finally
        {
            await _provider.DisposeAsync();
        }
    }

    /// <summary>
    /// Disposes the app's services. Prefer <see cref="DisposeAsync"/>: this one throws when a service the app created
    /// disposes only asynchronously.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A service the app created implements only <see cref="IAsyncDisposable"/>.
    /// </exception>
    public void Dispose()
    {
        try
        {
            _scope.Dispose();
        }
        finally
        {
            _provider.Dispose();
        }
    }
}
