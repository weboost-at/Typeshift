using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

/// <summary>
/// Creates the runtime of an app without a container. An app with a container gets its <see cref="ITypeshiftRuntime"/>
/// from <c>services.AddTypeshift()</c> instead.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-runtime">The runtime</see>.
/// </summary>
public static class TypeshiftRuntime
{
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
    /// <returns>The runtime, which your app disposes at its end.</returns>
    /// <exception cref="InvalidOperationException">The call was not intercepted.</exception>
    public static ITypeshiftRuntime Create(IChatClient client, Action<TypeshiftBuilder>? configure = null) =>
        throw new InvalidOperationException(
            "TypeshiftRuntime.Create() was not intercepted by the Typeshift generator. Reference the Typeshift package "
            + "in the project that calls it, call it as TypeshiftRuntime.Create(client), not through using static, a "
            + "method group or a dynamic argument, and keep the Typeshift.Generated namespace in "
            + "InterceptorsNamespaces.");
}

// D75, D76: the runtime behind ITypeshiftRuntime, one per container, which owns the container Create built
internal sealed class Runtime : ITypeshiftRuntime
{
    private ServiceProvider? _container;

    // D83: read on first use, so a runtime that lists nothing creates no agent and no toolset for it
    private readonly Lazy<RuntimeCatalog> _catalog;

    public Runtime(IServiceProvider services)
    {
        Services = services;
        _catalog = new(() => new RuntimeCatalog(services));
    }

    public IServiceProvider Services { get; }

    public IReadOnlyList<AgentInfo> Agents => _catalog.Value.Agents;

    public IReadOnlyList<ToolsetInfo> Toolsets => _catalog.Value.Toolsets;

    // D76: a unit of work, whose scope the host owns, so the factory is named for the disposable it creates
    public AgentHost<TAgent> CreateHost<TAgent>() where TAgent : Agent =>
        new(AgentHost<TAgent>.Registered(Services), Services.CreateAsyncScope());

    // D83: the kind is unique and stable, where a name may repeat (D22, D39)
    public IAgentHost CreateHost(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        var agent = _catalog.Value.Find(kind)
            ?? throw new ArgumentException(
                $"No agent of the kind '{kind}' is registered. The kinds are those of the runtime's Agents: "
                + $"{string.Join(", ", Agents.Select(each => $"'{each.Kind}'"))}.",
                nameof(kind));

        return agent.Host(Services.CreateAsyncScope());
    }

    public IAgentHost CreateHost(Type agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var registered = _catalog.Value.Find(agent)
            ?? throw new ArgumentException(
                $"The agent '{agent.Name}' is not registered. The agents are those of the runtime's Agents.",
                nameof(agent));

        return registered.Host(Services.CreateAsyncScope());
    }

    // D76: disposing the container disposes this runtime again, which then finds nothing to dispose
    public ValueTask DisposeAsync() =>
        Interlocked.Exchange(ref _container, null)?.DisposeAsync() ?? ValueTask.CompletedTask;

    public void Dispose() => Interlocked.Exchange(ref _container, null)?.Dispose();

    // D76: the container's own runtime, which from now on owns the container
    public static ITypeshiftRuntime Owning(ServiceProvider container)
    {
        var runtime = container.GetRequiredService<Runtime>();
        runtime._container = container;

        return runtime;
    }
}
