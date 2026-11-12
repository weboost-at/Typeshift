using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

/// <summary>
/// The root of Typeshift in your app, which hands out the host of every agent of this project and of the projects it
/// references. An app without a container creates it with <see cref="Create"/> and disposes it at the end. An app
/// with a container gets it from <c>services.AddTypeshift()</c>, as a singleton, and injects it where no request
/// scope is at hand, such as in a background service. Each host it hands out is one unit of work, with scoped
/// services of its own.
/// </summary>
public sealed class TypeshiftRuntime : IAsyncDisposable, IDisposable
{
    private ServiceProvider? _container;

    internal TypeshiftRuntime(IServiceProvider services) => Services = services;

    /// <summary>
    /// The root services, for singletons <see cref="Agent{TAgent}"/> does not reach, such as the
    /// <c>ConversationTracker</c>. Scoped services resolve only from a scope.
    /// </summary>
    public IServiceProvider Services { get; }

    // D1, D75: the generator intercepts every call; this body runs only for one it did not see
    /// <summary>
    /// Creates the runtime of an app without a container, with every toolset and agent of this project and of the
    /// projects it references, and with <paramref name="client"/> serving every agent's runs. The runtime owns the
    /// container it builds. The generator intercepts the call; one it did not intercept throws, saying what to check.
    /// </summary>
    /// <param name="client">The chat client of every run; the runtime never disposes it.</param>
    /// <param name="configure">
    /// Registers what else the app needs, as the chain of <c>AddTypeshift()</c> does, such as
    /// <c>typeshift => typeshift.WithStore&lt;MyStore&gt;()</c>; null for nothing.
    /// </param>
    /// <returns>The runtime.</returns>
    /// <exception cref="InvalidOperationException">The call was not intercepted.</exception>
    public static TypeshiftRuntime Create(IChatClient client, Action<TypeshiftBuilder>? configure = null) =>
        throw new InvalidOperationException(
            "TypeshiftRuntime.Create() was not intercepted by the Typeshift generator. Reference the Typeshift package "
            + "in the project that calls it, call it as TypeshiftRuntime.Create(client), not through using static, a "
            + "method group or a dynamic argument, and keep the Typeshift.Generated namespace in "
            + "InterceptorsNamespaces.");

    // D76: a unit of work, whose scope the host owns
    /// <summary>
    /// Creates the host of an agent, ready to run, in a scope of its own: its tools, its chat client provider, its
    /// middleware and every other scoped service resolve there, apart from other hosts'. Dispose the host when its
    /// work is done, which disposes the scope.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <returns>The host, with the agent's typed members.</returns>
    public AgentHost<TAgent> Agent<TAgent>() where TAgent : Agent =>
        new(Services.GetRequiredService<TAgent>(), Services.CreateAsyncScope());

    // D76: disposing the container disposes this runtime again, which then finds nothing to dispose
    /// <summary>
    /// Disposes the container of a runtime <see cref="Create"/> returned, which ends what its services hold, such as
    /// stores your app did not create. A runtime your app's container created leaves the container alone.
    /// </summary>
    /// <returns>The disposal.</returns>
    public ValueTask DisposeAsync() =>
        Interlocked.Exchange(ref _container, null)?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>
    /// Disposes the container of a runtime <see cref="Create"/> returned. Prefer <see cref="DisposeAsync"/>: this one
    /// throws when a service the container created disposes only asynchronously.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A service the container created implements only <see cref="IAsyncDisposable"/>.
    /// </exception>
    public void Dispose() => Interlocked.Exchange(ref _container, null)?.Dispose();

    // D76: the container's own runtime, which from now on owns the container
    internal static TypeshiftRuntime Owning(ServiceProvider container)
    {
        var runtime = container.GetRequiredService<TypeshiftRuntime>();
        runtime._container = container;

        return runtime;
    }
}
