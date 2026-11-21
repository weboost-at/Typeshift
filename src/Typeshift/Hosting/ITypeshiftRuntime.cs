namespace Typeshift;

// D76: the root of every app, an interface over a class only the library creates, so an app injects it and a test
// fakes it
/// <summary>
/// The root of Typeshift in your app, which hands out the host of every agent of this project and of the projects it
/// references. An app without a container creates it with <see cref="TypeshiftRuntime.Create"/> and disposes it at the
/// end. An app with a container gets it from <c>services.AddTypeshift()</c>, as a singleton, and injects it where no
/// request scope is at hand, such as in a background service. Each host it hands out is one unit of work, with scoped
/// services of its own. Disposing a runtime <c>Create</c> returned disposes the container it built, and with it the
/// services the container created; disposing the one of your app's container does nothing.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-runtime">The runtime</see>.
/// </summary>
public interface ITypeshiftRuntime : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The root services, for singletons <see cref="CreateHost{TAgent}"/> does not reach, such as a
    /// <see cref="ConversationTracker{TId}"/>. Scoped services resolve only from a scope.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// The agents of this project and of the projects it references, each with its type, name, kind and
    /// description, for a page that lists them, or an endpoint that takes the kind of the agent it runs.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The catalog</see>.
    /// </summary>
    IReadOnlyList<AgentInfo> Agents { get; }

    /// <summary>
    /// The toolsets of this project and of the projects it references, each with its display name and its tools,
    /// for a tool picker.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The catalog</see>.
    /// </summary>
    IReadOnlyList<ToolsetInfo> Toolsets { get; }

    /// <summary>
    /// Creates the host of an agent, ready to run, in a scope of its own: its tools, its middleware and every other
    /// scoped service resolve there, apart from other hosts'. Dispose the host, or a copy of it from one of its
    /// <c>With...</c> calls, once its runs and streams are done, which disposes the scope.
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
    AgentHost<TAgent> CreateHost<TAgent>() where TAgent : Agent;

    /// <summary>
    /// Creates the host of the agent of a kind, as <see cref="CreateHost{TAgent}"/> creates one, for code that reads
    /// the agent from a request or from configuration. Dispose it once its runs and streams are done.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The catalog</see>.
    /// </summary>
    /// <param name="kind">
    /// The kind of one of <see cref="Agents"/>: its <c>WithStorageKind</c>, or its class name.
    /// </param>
    /// <returns>The host, without the members that take the agent's own <c>Settings</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kind"/> is null.</exception>
    /// <exception cref="ArgumentException">No agent of the kind is registered.</exception>
    IAgentHost CreateHost(string kind);

    /// <summary>
    /// Creates the host of the agent of a type, as <see cref="CreateHost{TAgent}"/> creates one, for code that holds
    /// the agent's <see cref="Type"/> rather than naming it. Dispose it once its runs and streams are done.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The catalog</see>.
    /// </summary>
    /// <param name="agent">The type of one of <see cref="Agents"/>.</param>
    /// <returns>The host, without the members that take the agent's own <c>Settings</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is null.</exception>
    /// <exception cref="ArgumentException">No module registered an agent of the type.</exception>
    IAgentHost CreateHost(Type agent);
}
