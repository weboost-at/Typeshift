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
    /// The root services, for singletons <see cref="CreateHost{TAgent}"/> does not reach, such as a
    /// <see cref="ConversationTracker{TId}"/>. Scoped services resolve only from a scope.
    /// </summary>
    public IServiceProvider Services { get; }

    // D1, D75: the generator intercepts every call; this body runs only for one it did not see
    /// <summary>
    /// Creates the runtime of an app without a container, with every toolset and agent of this project and of the
    /// projects it references, and with <paramref name="client"/> as the default client, which serves every run
    /// that names no provider. The runtime owns the container it builds. The generator intercepts the call; one it
    /// did not intercept throws, saying what to check.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-runtime">The runtime</see>.
    /// </summary>
    /// <param name="client">
    /// The default chat client. An agent that names a provider needs a client registered for that provider, through
    /// <paramref name="configure"/>. The runtime never disposes the client.
    /// </param>
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

    // D76: a unit of work, whose scope the host owns, so the factory is named for the disposable it creates
    /// <summary>
    /// Creates the host of an agent, ready to run, in a scope of its own: its tools, its chat client provider, its
    /// middleware and every other scoped service resolve there, apart from other hosts'. Dispose the host, or a copy
    /// of it from <c>WithSettingsId</c> or <c>WithConversationId</c>, once its runs and streams are done, which
    /// disposes the scope.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/using-an-agent#run-from-a-background-job">Run from a
    /// background job</see>.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <returns>The host, with the agent's typed members.</returns>
    /// <exception cref="InvalidOperationException">
    /// No module registered the agent: the app's <c>AddTypeshift()</c> lies only in a library it references, or the
    /// agent is abstract or of a project the generator did not write code for.
    /// </exception>
    public AgentHost<TAgent> CreateHost<TAgent>() where TAgent : Agent =>
        new(AgentHost<TAgent>.Registered(Services), Services.CreateAsyncScope());

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
